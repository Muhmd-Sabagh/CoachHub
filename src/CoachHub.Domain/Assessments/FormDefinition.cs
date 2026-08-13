using CoachHub.Domain.Common;

namespace CoachHub.Domain.Assessments;

public sealed class FormDefinition : Entity
{
    private FormDefinition() { }
    public string Name { get; private set; } = string.Empty;
    public AssessmentFormType FormType { get; private set; }
    public bool IsArchived { get; private set; }

    public static FormDefinition Create(string name, AssessmentFormType formType)
    {
        var item = new FormDefinition { FormType = formType };
        item.Update(name);
        return item;
    }
    public void Update(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        name = name.Trim();
        if (name.Length > 200) throw new ArgumentOutOfRangeException(nameof(name));
        Name = name;
    }
    public void SetArchived(bool archived) => IsArchived = archived;
}