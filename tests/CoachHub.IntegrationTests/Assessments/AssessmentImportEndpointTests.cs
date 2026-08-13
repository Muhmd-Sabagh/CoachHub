using System.IO.Compression;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security;
using System.Text;
using CoachHub.Application.Assessments;
using CoachHub.Application.Assessments.Importing;
using CoachHub.Application.Auth.Login;
using CoachHub.Application.Clients;
using CoachHub.Domain.Assessments;
using CoachHub.Infrastructure.Persistence;
using CoachHub.IntegrationTests.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoachHub.IntegrationTests.Assessments;

public sealed class AssessmentImportEndpointTests : IClassFixture<CoachHubApiFactory>
{
    private readonly CoachHubApiFactory _factory;
    private readonly HttpClient _client;

    public AssessmentImportEndpointTests(CoachHubApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Xlsx_import_uses_stable_mappings_shared_domain_diagnostics_and_deduplication()
    {
        await AuthenticateAsync();
        var client = await PostAsync<ClientResponse>("/api/clients",
            new ClientCreateInput("Import Client " + Guid.NewGuid(), null, null, null));
        var form = await PostAsync<FormVersionResponse>("/api/assessment-forms",
            new CreateFormInput("Imported Updates " + Guid.NewGuid(), AssessmentFormType.UpdateAssessment));
        var questions = new[]
        {
            await AddQuestion(form.DefinitionId, "Short", QuestionType.ShortText, []),
            await AddQuestion(form.DefinitionId, "Long", QuestionType.LongText, []),
            await AddQuestion(form.DefinitionId, "Number", QuestionType.Number, []),
            await AddQuestion(form.DefinitionId, "Date", QuestionType.Date, []),
            await AddQuestion(form.DefinitionId, "Boolean", QuestionType.Boolean, []),
            await AddQuestion(form.DefinitionId, "Single", QuestionType.SingleChoice,
                [new("a", "Alpha", 0), new("b", "Beta", 1)]),
            await AddQuestion(form.DefinitionId, "Multiple", QuestionType.MultipleChoice,
                [new("a", "Alpha", 0), new("b", "Beta", 1)]),
            await AddQuestion(form.DefinitionId, "Media", QuestionType.MediaUpload, [])
        };
        await PostEmptyAsync<FormVersionResponse>($"/api/assessment-forms/{form.DefinitionId}/publish");
        var headers = new[]
        {
            "Form Code", "Timestamp", "Response ID", "Short Header", "Long Header",
            "Number Header", "Date Header", "Boolean Header", "Single Header",
            "Multiple Header", "Media Header", "Unexpected Question"
        };
        var profile = await PostAsync<ImportProfileResponse>("/api/assessment-imports/profiles",
            new SaveImportProfileInput(form.DefinitionId, "Google Export", "Form Responses 1",
                "Form Code", "Timestamp", "Response ID",
                questions.Select((question, index) => new ImportColumnMappingInput(
                    "column-" + index, headers[index + 3], question.StableKey)).ToArray()));

        var valid = new[]
        {
            client.FormCode, "45824.5", "response-1", "short", "long notes", "82.5",
            "2025-01-02", "لا", "Alpha", "Alpha, Beta",
            "https://drive.google.com/open?id=assessment-photo", "ignored"
        };
        var duplicate = valid.ToArray(); duplicate[1] = "45825.5";
        var unknown = valid.ToArray(); unknown[0] = "UNKNOWN9999"; unknown[2] = "response-2";
        var invalid = valid.ToArray(); invalid[2] = "response-3"; invalid[5] = "not-a-number";
        await using var workbook = CreateWorkbook(headers, [valid, duplicate, unknown, invalid]);
        using var multipart = new MultipartFormDataContent();
        using var file = new StreamContent(workbook);
        file.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        multipart.Add(file, "file", "google-responses.xlsx");
        var response = await _client.PostAsync($"/api/assessment-imports/profiles/{profile.Id}/imports", multipart);
        response.EnsureSuccessStatusCode();
        var summary = (await response.Content.ReadFromJsonAsync<AssessmentImportSummary>())!;

        Assert.Equal(1, summary.Imported);
        Assert.Equal(1, summary.SkippedDuplicate);
        Assert.Equal(1, summary.Invalid);
        Assert.Equal(1, summary.UnmappedQuestion);
        Assert.Equal(1, summary.UnknownClientOrCode);
        Assert.Contains(summary.Diagnostics, item => item.RowNumber == 1 && item.Status == ImportRowStatus.UnmappedQuestion);
        Assert.Contains(summary.Diagnostics, item => item.RowNumber == 5 && item.Status == ImportRowStatus.Invalid && item.Column == "Number Header");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoachHubDbContext>();
        var submission = await db.Set<FormSubmission>().SingleAsync(item =>
            item.FormDefinitionId == form.DefinitionId && item.ClientId == client.Id);
        Assert.Equal(SubmissionSource.GoogleFormsExcelImport, submission.Source);
        Assert.Equal("response-1", submission.ExternalSubmissionId);
        Assert.Equal(64, submission.ImportFingerprint!.Length);
        Assert.Equal(2025, submission.SubmittedAt.Year);
        var answers = await db.Set<FormAnswer>().Where(item => item.FormSubmissionId == submission.Id).ToArrayAsync();
        Assert.Equal(8, answers.Length);
        Assert.Equal("https://drive.google.com/open?id=assessment-photo",
            answers.Single(item => item.QuestionTypeSnapshot == QuestionType.MediaUpload).ExternalMediaUrl);
    }

    private async Task<QuestionResponse> AddQuestion(
        Guid definitionId, string text, QuestionType type, IReadOnlyList<OptionInput> options) =>
        await PostAsync<QuestionResponse>($"/api/assessment-forms/{definitionId}/questions",
            new QuestionInput(null, text, type, true, Array.IndexOf(
                new[] { "Short", "Long", "Number", "Date", "Boolean", "Single", "Multiple", "Media" }, text), options));

    private async Task AuthenticateAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = CoachHubApiFactory.AdminEmail, password = CoachHubApiFactory.AdminPassword });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResult>();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
    }
    private async Task<T> PostAsync<T>(string url, object input)
    {
        var response = await _client.PostAsJsonAsync(url, input);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
    private async Task<T> PostEmptyAsync<T>(string url)
    {
        var response = await _client.PostAsync(url, null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static MemoryStream CreateWorkbook(IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "xl/workbook.xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                  xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="Form Responses 1" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """);
            Write(archive, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
                </Relationships>
                """);
            Write(archive, "xl/styles.xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <numFmts count="0"/><fonts count="1"><font/></fonts><fills count="1"><fill/></fills>
                  <borders count="1"><border/></borders><cellStyleXfs count="1"><xf numFmtId="0"/></cellStyleXfs>
                  <cellXfs count="2"><xf numFmtId="0"/><xf numFmtId="22" applyNumberFormat="1"/></cellXfs>
                </styleSheet>
                """);
            var xml = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
            xml.Append("<row r=\"1\">");
            for (var column = 0; column < headers.Count; column++)
                xml.Append(InlineCell(CellReference(column, 1), headers[column]));
            xml.Append("</row>");
            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var number = rowIndex + 2;
                xml.Append($"<row r=\"{number}\">");
                for (var column = 0; column < rows[rowIndex].Length; column++)
                {
                    if (column == 1)
                        xml.Append($"<c r=\"{CellReference(column, number)}\" s=\"1\"><v>{rows[rowIndex][column]}</v></c>");
                    else
                        xml.Append(InlineCell(CellReference(column, number), rows[rowIndex][column]));
                }
                xml.Append("</row>");
            }
            xml.Append("</sheetData></worksheet>");
            Write(archive, "xl/worksheets/sheet1.xml", xml.ToString());
        }
        stream.Position = 0;
        return stream;
    }
    private static string InlineCell(string reference, string value) =>
        $"<c r=\"{reference}\" t=\"inlineStr\"><is><t>{SecurityElement.Escape(value)}</t></is></c>";
    private static string CellReference(int column, int row)
    {
        var name = string.Empty;
        for (var value = column + 1; value > 0; value = (value - 1) / 26)
            name = (char)('A' + (value - 1) % 26) + name;
        return name + row;
    }
    private static void Write(ZipArchive archive, string path, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(path).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}