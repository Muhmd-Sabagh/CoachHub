using CoachHub.Domain.Assessments;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoachHub.Infrastructure.Assessments;

public sealed class FormDefinitionConfiguration : IEntityTypeConfiguration<FormDefinition>
{
    public void Configure(EntityTypeBuilder<FormDefinition> b)
    { b.ToTable("FormDefinitions"); b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(200).IsRequired(); b.Property(x => x.FormType).HasConversion<string>().HasMaxLength(30); }
}
public sealed class FormVersionConfiguration : IEntityTypeConfiguration<FormVersion>
{
    public void Configure(EntityTypeBuilder<FormVersion> b)
    { b.ToTable("FormVersions"); b.HasKey(x => x.Id); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20); b.HasIndex(x => new { x.FormDefinitionId, x.VersionNumber }).IsUnique(); b.HasIndex(x => x.FormDefinitionId).HasFilter("[Status] = 'Draft'").IsUnique(); b.HasOne<FormDefinition>().WithMany().HasForeignKey(x => x.FormDefinitionId).OnDelete(DeleteBehavior.Cascade); }
}
public sealed class FormSectionConfiguration : IEntityTypeConfiguration<FormSection>
{
    public void Configure(EntityTypeBuilder<FormSection> b)
    { b.ToTable("FormSections"); b.HasKey(x => x.Id); b.Property(x => x.Title).HasMaxLength(200); b.HasIndex(x => new { x.FormVersionId, x.Order }); b.HasOne<FormVersion>().WithMany().HasForeignKey(x => x.FormVersionId).OnDelete(DeleteBehavior.Cascade); }
}
public sealed class FormQuestionConfiguration : IEntityTypeConfiguration<FormQuestion>
{
    public void Configure(EntityTypeBuilder<FormQuestion> b)
    { b.ToTable("FormQuestions"); b.HasKey(x => x.Id); b.Property(x => x.Text).HasMaxLength(1000); b.Property(x => x.QuestionType).HasConversion<string>().HasMaxLength(30); b.HasIndex(x => new { x.FormVersionId, x.Order }); b.HasIndex(x => new { x.FormVersionId, x.StableKey }).IsUnique(); b.HasOne<FormVersion>().WithMany().HasForeignKey(x => x.FormVersionId).OnDelete(DeleteBehavior.Cascade); b.HasOne<FormSection>().WithMany().HasForeignKey(x => x.FormSectionId).OnDelete(DeleteBehavior.NoAction); }
}
public sealed class QuestionOptionConfiguration : IEntityTypeConfiguration<QuestionOption>
{
    public void Configure(EntityTypeBuilder<QuestionOption> b)
    { b.ToTable("QuestionOptions"); b.HasKey(x => x.Id); b.Property(x => x.Value).HasMaxLength(100); b.Property(x => x.Label).HasMaxLength(500); b.HasIndex(x => new { x.FormQuestionId, x.Value }).IsUnique(); b.HasOne<FormQuestion>().WithMany().HasForeignKey(x => x.FormQuestionId).OnDelete(DeleteBehavior.Cascade); }
}
public sealed class FormSubmissionConfiguration : IEntityTypeConfiguration<FormSubmission>
{
    public void Configure(EntityTypeBuilder<FormSubmission> b)
    { b.ToTable("FormSubmissions"); b.HasKey(x => x.Id); b.Property(x => x.FormType).HasConversion<string>().HasMaxLength(30); b.Property(x => x.Source).HasConversion<string>().HasMaxLength(40); b.HasIndex(x => x.InitialClientId).HasFilter("[InitialClientId] IS NOT NULL").IsUnique(); b.HasIndex(x => new { x.ClientId, x.SubmittedAt }); b.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade); b.HasOne<FormDefinition>().WithMany().HasForeignKey(x => x.FormDefinitionId).OnDelete(DeleteBehavior.NoAction); b.HasOne<FormVersion>().WithMany().HasForeignKey(x => x.FormVersionId).OnDelete(DeleteBehavior.NoAction); }
}
public sealed class FormAnswerConfiguration : IEntityTypeConfiguration<FormAnswer>
{
    public void Configure(EntityTypeBuilder<FormAnswer> b)
    { b.ToTable("FormAnswers"); b.HasKey(x => x.Id); b.Property(x => x.QuestionTextSnapshot).HasMaxLength(1000); b.Property(x => x.QuestionTypeSnapshot).HasConversion<string>().HasMaxLength(30); b.Property(x => x.ValueJson).HasColumnType("nvarchar(max)"); b.HasIndex(x => new { x.FormSubmissionId, x.FormQuestionId }).IsUnique(); b.HasOne<FormSubmission>().WithMany().HasForeignKey(x => x.FormSubmissionId).OnDelete(DeleteBehavior.Cascade); b.HasOne<FormQuestion>().WithMany().HasForeignKey(x => x.FormQuestionId).OnDelete(DeleteBehavior.NoAction); b.HasOne<MediaAsset>().WithMany().HasForeignKey(x => x.MediaId).OnDelete(DeleteBehavior.Restrict); }
}