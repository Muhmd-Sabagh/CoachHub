using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Domain.Assessments;
using CoachHub.Domain.Billing;
using CoachHub.Domain.CheckIns;
using CoachHub.Domain.Clients;
using CoachHub.Domain.FoodTracking;
using CoachHub.Domain.Progress;
using CoachHub.Domain.WorkoutTracking;

namespace CoachHub.Application.Exports;

/// <summary>What a coach exports for one client (§51); <see cref="All"/> is every dataset in one ZIP.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ClientExport>))]
public enum ClientExport
{
    Client,
    Progress,
    Nutrition,
    Workouts,
    CheckIns,
    All
}

/// <summary>What the Administrator exports across the platform (owner decision 2026-10-22).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlatformExport>))]
public enum PlatformExport
{
    Coaches,
    Clients,
    Subscriptions,
    Revenue
}

public sealed record ExportFile(byte[] Content, string FileName, string ContentType);

public sealed record ExportClient(Client Client, string CoachName);

public sealed record ExportCoach(Guid Id, string Name, bool IsActive, DateTimeOffset CreatedAt, int Clients);

public sealed record ExportSubscription(Subscription Subscription, string ClientName, string ClientCode, string CoachName, string PackageName, string CurrencyCode);

/// <summary>A settled payment or a refund (D6), dated when it was recorded.</summary>
public sealed record LedgerEntry(DateTimeOffset RecordedAt, bool IsRefund, string CoachName, string ClientName, string InvoiceNumber,
    string CurrencyCode, decimal Amount, string? PaymentAccount, string? Note);

public sealed record ExportProgress(
    IReadOnlyList<WeightEntry> Weights, IReadOnlyList<BodyFatEntry> BodyFat, IReadOnlyList<BodyMeasurement> Measurements,
    IReadOnlyList<SleepEntry> Sleep, IReadOnlyList<StepsEntry> Steps, IReadOnlyList<WaterEntry> Water);

public sealed record ExportCheckIn(CheckIn CheckIn, string FormName, IReadOnlyList<FormAnswer> Answers);

/// <summary>Export reads. Every query runs under the tenant filters: a coach exports their own clients only.</summary>
public interface IExportRepository
{
    Task<IReadOnlyList<ExportClient>> ClientsAsync(CancellationToken cancellationToken);
    Task<(Client Client, ClientProfile? Profile, string CoachName)?> ClientAsync(Guid clientId, CancellationToken cancellationToken);
    Task<ExportProgress> ProgressAsync(Guid clientId, CancellationToken cancellationToken);
    Task<IReadOnlyList<FoodLogEntry>> FoodLogAsync(Guid clientId, CancellationToken cancellationToken);
    Task<(IReadOnlyList<WorkoutSession> Sessions, IReadOnlyDictionary<Guid, string> ExerciseNames)> WorkoutsAsync(Guid clientId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExportCheckIn>> CheckInsAsync(Guid clientId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExportCoach>> CoachesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ExportSubscription>> SubscriptionsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, SubscriptionPaymentStatus>> SubscriptionPaymentsAsync(IReadOnlyCollection<Guid> subscriptionIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<LedgerEntry>> LedgerAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>Records the export in the audit trail (D2, §58): personal data left the platform.</summary>
    Task RecordAsync(string dataset, Guid? clientId, Guid? coachId, int rows, CancellationToken cancellationToken);
}

/// <summary>
/// Data export (§51): CSV per dataset. The coach exports their client list and, per client, client data, progress,
/// nutrition, workout history and check-ins (one CSV or all in a ZIP); the Administrator exports coaches, clients,
/// subscriptions and revenue across the platform. Reports export as PDF (<c>ReportService</c>). Every export is
/// audited.
/// </summary>
public sealed class ExportService(IExportRepository repository, TimeProvider timeProvider)
{
    public const int MaximumRevenueDays = 366;

    /// <summary>The coach's clients (administrators: every client), with their computed status.</summary>
    public async Task<ExportFile> ClientListAsync(CancellationToken cancellationToken)
    {
        var today = Today();
        var clients = await repository.ClientsAsync(cancellationToken);
        var file = new ExportFile(Csv.Write(
            ["Client code", "Name", "Phone", "Email", "Join date", "Active", "Status", "Subscription", "Coach"],
            clients.OrderBy(x => x.Client.Name).Select(x => (IReadOnlyList<object?>)[
                x.Client.ClientCode, x.Client.Name, x.Client.Phone, x.Client.Email, x.Client.JoinDate, x.Client.IsActive,
                x.Client.GetStatus(today), x.Client.GetSubscriptionStatus(today), x.CoachName])),
            $"clients-{today:yyyy-MM-dd}.csv", "text/csv");
        await repository.RecordAsync("ClientList", null, null, clients.Count, cancellationToken);
        return file;
    }

    public async Task<ExportFile> ClientAsync(Guid clientId, ClientExport dataset, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(dataset)) throw new ValidationException(new Dictionary<string, string[]> { ["dataset"] = ["Unknown export."] });
        var found = await repository.ClientAsync(clientId, cancellationToken) ?? throw new NotFoundException("Client", clientId);
        var prefix = $"{found.Client.ClientCode.ToLowerInvariant()}";
        var parts = dataset == ClientExport.All
            ? Enum.GetValues<ClientExport>().Where(x => x != ClientExport.All).ToArray()
            : [dataset];
        var files = new List<(string Name, byte[] Content, int Rows)>();
        foreach (var part in parts)
        {
            var (content, rows) = part switch
            {
                ClientExport.Client => ClientData(found.Client, found.Profile, found.CoachName),
                ClientExport.Progress => ProgressCsv(await repository.ProgressAsync(clientId, cancellationToken)),
                ClientExport.Nutrition => NutritionCsv(await repository.FoodLogAsync(clientId, cancellationToken)),
                ClientExport.Workouts => WorkoutsCsv(await repository.WorkoutsAsync(clientId, cancellationToken)),
                _ => CheckInsCsv(await repository.CheckInsAsync(clientId, cancellationToken))
            };
            files.Add(($"{prefix}-{part.ToString().ToLowerInvariant()}.csv", content, rows));
        }
        await repository.RecordAsync(dataset.ToString(), clientId, found.Client.CoachId, files.Sum(x => x.Rows), cancellationToken);
        if (dataset != ClientExport.All) return new ExportFile(files[0].Content, files[0].Name, "text/csv");

        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content, _) in files)
            {
                await using var entry = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
                await entry.WriteAsync(content, cancellationToken);
            }
        }
        return new ExportFile(buffer.ToArray(), $"{prefix}-export-{Today():yyyy-MM-dd}.zip", "application/zip");
    }

    /// <summary>Platform exports for the Administrator; revenue covers <paramref name="from"/>–<paramref name="to"/>.</summary>
    public async Task<ExportFile> PlatformAsync(PlatformExport dataset, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var today = Today();
        (byte[] Content, int Rows) csv;
        string name;
        switch (dataset)
        {
            case PlatformExport.Coaches:
                var coaches = await repository.CoachesAsync(cancellationToken);
                csv = (Csv.Write(["Coach", "Active", "Created", "Clients"],
                    coaches.OrderBy(x => x.Name).Select(x => (IReadOnlyList<object?>)[x.Name, x.IsActive, x.CreatedAt, x.Clients])), coaches.Count);
                name = "coaches";
                break;
            case PlatformExport.Clients:
                var clients = await repository.ClientsAsync(cancellationToken);
                csv = (Csv.Write(["Coach", "Client code", "Name", "Email", "Join date", "Active", "Status", "Subscription"],
                    clients.OrderBy(x => x.CoachName).ThenBy(x => x.Client.Name).Select(x => (IReadOnlyList<object?>)[
                        x.CoachName, x.Client.ClientCode, x.Client.Name, x.Client.Email, x.Client.JoinDate, x.Client.IsActive,
                        x.Client.GetStatus(today), x.Client.GetSubscriptionStatus(today)])), clients.Count);
                name = "clients";
                break;
            case PlatformExport.Subscriptions:
                var subscriptions = await repository.SubscriptionsAsync(cancellationToken);
                var payments = await repository.SubscriptionPaymentsAsync(subscriptions.Select(x => x.Subscription.Id).ToArray(), cancellationToken);
                csv = (Csv.Write(["Coach", "Client code", "Client", "Package", "Start", "End", "Months", "Status", "Payment", "Price", "Currency", "Renewals", "Suspension reason"],
                    subscriptions.OrderBy(x => x.CoachName).ThenBy(x => x.ClientName).ThenBy(x => x.Subscription.StartDate).Select(x => (IReadOnlyList<object?>)[
                        x.CoachName, x.ClientCode, x.ClientName, x.PackageName, x.Subscription.StartDate, x.Subscription.EndDate,
                        x.Subscription.DurationMonths, x.Subscription.StatusOn(today),
                        payments.GetValueOrDefault(x.Subscription.Id, SubscriptionPaymentStatus.NoInvoice), x.Subscription.Price, x.CurrencyCode,
                        x.Subscription.RenewalCount, x.Subscription.SuspensionReason])), subscriptions.Count);
                name = "subscriptions";
                break;
            case PlatformExport.Revenue:
                var end = to ?? today;
                var start = from ?? end.AddDays(-29);
                if (start > end || end.DayNumber - start.DayNumber >= MaximumRevenueDays)
                    throw new ValidationException(new Dictionary<string, string[]> { ["period"] = [$"Choose an ordered period of at most {MaximumRevenueDays} days."] });
                var ledger = await repository.LedgerAsync(start, end, cancellationToken);
                // The revenue ledger (D6): payments settled minus refunds recorded, so the amounts sum to net revenue.
                csv = (Csv.Write(["Recorded (UTC)", "Type", "Coach", "Client", "Invoice", "Currency", "Amount", "Payment account", "Note"],
                    ledger.OrderBy(x => x.RecordedAt).Select(x => (IReadOnlyList<object?>)[
                        x.RecordedAt, x.IsRefund ? "Refund" : "Payment", x.CoachName, x.ClientName, x.InvoiceNumber, x.CurrencyCode,
                        x.IsRefund ? -x.Amount : x.Amount, x.PaymentAccount, x.Note])), ledger.Count);
                name = $"revenue-{start:yyyy-MM-dd}-to-{end:yyyy-MM-dd}";
                break;
            default:
                throw new ValidationException(new Dictionary<string, string[]> { ["dataset"] = ["Unknown export."] });
        }
        await repository.RecordAsync("Platform" + dataset, null, null, csv.Rows, cancellationToken);
        return new ExportFile(csv.Content, dataset == PlatformExport.Revenue ? name + ".csv" : $"{name}-{today:yyyy-MM-dd}.csv", "text/csv");
    }

    private static (byte[], int) ClientData(Client client, ClientProfile? profile, string coachName)
    {
        (string Field, object? Value)[] rows =
        [
            ("Client code", client.ClientCode), ("Name", client.Name), ("Phone", client.Phone), ("Email", client.Email),
            ("Join date", client.JoinDate), ("Active", client.IsActive), ("Coach", coachName),
            ("Date of birth", profile?.DateOfBirth), ("Sex", profile?.Sex), ("Height (cm)", profile?.HeightCm),
            ("Starting weight (kg)", profile?.StartingWeightKg), ("Starting body fat (%)", profile?.StartingBodyFatPercent),
            ("Goal", profile?.Goal), ("Activity level", profile?.ActivityLevel), ("Training days per week", profile?.TrainingDaysPerWeek),
            ("Average daily steps", profile?.AverageDailySteps), ("Training location", profile?.TrainingLocation),
            ("Training preferences", profile?.TrainingPreferences), ("Food preferences", profile?.FoodPreferences),
            ("Preferred foods", profile?.PreferredFoodsNote), ("Disliked foods", profile?.DislikedFoodsNote),
            ("Dietary restrictions", profile?.DietaryRestrictions), ("Dietary notes", profile?.DietaryNotes),
            ("Preferred meal count", profile?.PreferredMealCount), ("Bedtime", profile?.Bedtime), ("Wake time", profile?.WakeTime),
        ];
        return (Csv.Write(["Field", "Value"], rows.Select(x => (IReadOnlyList<object?>)[x.Field, x.Value])), rows.Length);
    }

    private static (byte[], int) ProgressCsv(ExportProgress progress)
    {
        var rows = new List<(DateOnly Date, string Measure, object? Value, string Unit, EntrySource Source, string? Notes)>();
        rows.AddRange(progress.Weights.Select(x => (x.Date, "Weight", (object?)x.WeightKg, "kg", x.Source, x.Notes)));
        rows.AddRange(progress.BodyFat.Select(x => (x.Date, "Body fat", (object?)x.Percent, "%", x.Source, x.Notes)));
        rows.AddRange(progress.Measurements.Select(x => (x.Date, $"Measurement: {x.Site}", (object?)x.ValueCm, "cm", x.Source, x.Notes)));
        rows.AddRange(progress.Sleep.Select(x => (x.Date, "Sleep", (object?)x.Hours, "h", x.Source,
            x.Quality is { } quality ? $"Quality {quality}/5" + (x.Notes is null ? "" : " · " + x.Notes) : x.Notes)));
        rows.AddRange(progress.Steps.Select(x => (x.Date, "Steps", (object?)x.Steps, "steps", x.Source, x.Notes)));
        rows.AddRange(progress.Water.Select(x => (x.Date, "Water", (object?)x.AmountMl, "ml", x.Source, x.Notes)));
        return (Csv.Write(["Date", "Measure", "Value", "Unit", "Source", "Notes"],
            rows.OrderBy(x => x.Date).ThenBy(x => x.Measure).Select(x => (IReadOnlyList<object?>)[x.Date, x.Measure, x.Value, x.Unit, x.Source, x.Notes])), rows.Count);
    }

    private static (byte[], int) NutritionCsv(IReadOnlyList<FoodLogEntry> entries) =>
        (Csv.Write(["Date", "Meal", "Food", "Quantity", "Unit", "Calories", "Protein (g)", "Carbohydrates (g)", "Fat (g)", "Fiber (g)", "Source", "Logged (UTC)"],
            entries.OrderBy(x => x.Date).ThenBy(x => x.Slot).ThenBy(x => x.LoggedAt).Select(x => (IReadOnlyList<object?>)[
                x.Date, x.Slot, x.NameEn, x.Quantity ?? x.BaseAmount, x.UnitLabel, x.Calories, x.Protein, x.Carbohydrates, x.Fat, x.Fiber,
                x.Source, x.LoggedAt])), entries.Count);

    private static (byte[], int) WorkoutsCsv((IReadOnlyList<WorkoutSession> Sessions, IReadOnlyDictionary<Guid, string> ExerciseNames) data)
    {
        var rows = new List<IReadOnlyList<object?>>();
        foreach (var session in data.Sessions.OrderBy(x => x.Date).ThenBy(x => x.StartedAt))
        {
            foreach (var exercise in session.Exercises.OrderBy(x => x.Order))
            {
                var name = data.ExerciseNames.GetValueOrDefault(exercise.ExerciseId, "Exercise");
                if (exercise.Sets.Count == 0)
                    rows.Add([session.Date, session.NameEn, session.Status, name, exercise.Block, exercise.IsSkipped, null, null, null, null, null, null, null, null, exercise.Notes]);
                foreach (var set in exercise.Sets.OrderBy(x => x.Order))
                    rows.Add([session.Date, session.NameEn, session.Status, name, exercise.Block, exercise.IsSkipped, set.Order + 1, set.Type, set.Status,
                        set.Reps, set.WeightKg, set.Rir, set.Rpe, set.DurationSeconds, set.Notes ?? exercise.Notes]);
            }
        }
        return (Csv.Write(["Date", "Workout", "Workout status", "Exercise", "Block", "Skipped", "Set", "Set type", "Set status", "Reps", "Weight (kg)", "RIR", "RPE", "Duration (s)", "Notes"], rows), rows.Count);
    }

    private static (byte[], int) CheckInsCsv(IReadOnlyList<ExportCheckIn> checkIns)
    {
        var rows = new List<IReadOnlyList<object?>>();
        foreach (var item in checkIns.OrderBy(x => x.CheckIn.DueOn))
        {
            var checkIn = item.CheckIn;
            if (item.Answers.Count == 0) rows.Add([checkIn.DueOn, item.FormName, checkIn.SubmittedAt, checkIn.Status, checkIn.IsLate, null, null]);
            foreach (var answer in item.Answers)
                rows.Add([checkIn.DueOn, item.FormName, checkIn.SubmittedAt, checkIn.Status, checkIn.IsLate, answer.QuestionTextSnapshot,
                    AnswerText(answer.ValueJson, answer.MediaId is not null || answer.ExternalMediaUrl is not null)]);
        }
        // The coach's private notes are coaching notes, not the client's data, and are left out.
        return (Csv.Write(["Due", "Form", "Submitted (UTC)", "Status", "Late", "Question", "Answer"], rows), rows.Count);
    }

    /// <summary>An answer as text: the value, the values of a list, Yes/No, or "File" for an upload.</summary>
    public static string AnswerText(string valueJson, bool isFile)
    {
        if (isFile) return "File";
        try
        {
            using var document = JsonDocument.Parse(valueJson);
            var value = document.RootElement;
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "Yes",
                JsonValueKind.False => "No",
                JsonValueKind.Array => string.Join(", ", value.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.GetRawText())),
                JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                _ => value.GetRawText()
            };
        }
        catch (JsonException)
        {
            return valueJson;
        }
    }

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
}
