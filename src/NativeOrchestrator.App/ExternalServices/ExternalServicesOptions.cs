using System.ComponentModel.DataAnnotations;

namespace NativeOrchestrator.App.ExternalServices;

/// <summary>Base URLs of the external systems (section <c>ExternalServices</c>). They end with <c>/</c>.</summary>
public sealed class ExternalServicesOptions
{
  [Required]
  public Uri? Identity { get; set; }

  [Required]
  public Uri? Accounts { get; set; }

  [Required]
  public Uri? Notifications { get; set; }

  [Required]
  public Uri? Reports { get; set; }
}
