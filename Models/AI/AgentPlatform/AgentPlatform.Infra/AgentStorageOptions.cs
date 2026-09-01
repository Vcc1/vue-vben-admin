using System.ComponentModel.DataAnnotations;

namespace AgentPlatform.Infra;

public sealed class AgentStorageOptions
{
    public const string SectionName = "AgentPlatform:Storage";

    [Required]
    public string RootPath { get; set; } = "App_Data/agent-conversations";

    [Required]
    public string BusinessRootPath { get; set; } = "App_Data/hotel-business";
}
