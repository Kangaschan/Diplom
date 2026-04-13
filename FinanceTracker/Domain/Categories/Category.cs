using Domain.Common;

namespace Domain.Categories;

public sealed class Category : Entity
{
    public Guid? UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public CategoryType Type { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;
}
