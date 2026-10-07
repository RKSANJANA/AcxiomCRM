using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.Localization;

namespace Acxiom.Web.Support;

/// <summary>
/// Date must be today or later. Optionally ignored when another field has one of the given values
/// (e.g. an opportunity that's being closed as Won/Lost). Client half lives in wwwroot/js/rules.js.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class NotBeforeTodayAttribute : ValidationAttribute
{
    public string? UnlessField { get; set; }
    public string? UnlessValues { get; set; }

    public override bool RequiresValidationContext => true;

    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is not DateTime date) return ValidationResult.Success;

        if (UnlessField != null && UnlessValues != null)
        {
            var other = context.ObjectType.GetProperty(UnlessField)?.GetValue(context.ObjectInstance)?.ToString();
            if (other != null && UnlessValues.Split(',').Contains(other)) return ValidationResult.Success;
        }

        return date.Date >= DateTime.Today
            ? ValidationResult.Success
            : new ValidationResult(ErrorMessage ?? $"{context.DisplayName} can't be in the past.", new[] { context.MemberName! });
    }
}

public class NotBeforeTodayAdapter : AttributeAdapterBase<NotBeforeTodayAttribute>
{
    public NotBeforeTodayAdapter(NotBeforeTodayAttribute attribute, IStringLocalizer? localizer) : base(attribute, localizer) { }

    public override void AddValidation(ClientModelValidationContext context)
    {
        MergeAttribute(context.Attributes, "data-val", "true");
        MergeAttribute(context.Attributes, "data-val-today", GetErrorMessage(context));
        if (Attribute.UnlessField != null)
        {
            MergeAttribute(context.Attributes, "data-val-today-field", Attribute.UnlessField);
            MergeAttribute(context.Attributes, "data-val-today-values", Attribute.UnlessValues ?? "");
        }
    }

    public override string GetErrorMessage(ModelValidationContextBase context) =>
        Attribute.ErrorMessage ?? $"{context.ModelMetadata.GetDisplayName()} can't be in the past.";
}

/// <summary>Plugs the adapter above in front of the built-in ones.</summary>
public class CrmAdapterProvider : IValidationAttributeAdapterProvider
{
    private readonly ValidationAttributeAdapterProvider _builtIn = new();

    public IAttributeAdapter? GetAttributeAdapter(ValidationAttribute attribute, IStringLocalizer? localizer) =>
        attribute is NotBeforeTodayAttribute today
            ? new NotBeforeTodayAdapter(today, localizer)
            : _builtIn.GetAttributeAdapter(attribute, localizer);
}
