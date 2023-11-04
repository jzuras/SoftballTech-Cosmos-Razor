using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

// Razor does not play well with nullable reference types,
// but this line will still allow for null derefernce warnings
#nullable disable annotations

namespace Sbt.Models;

public class DivisionInfo
{
    [RegularExpression(@"^[a-zA-Z0-9]+[ a-zA-Z0-9-_]*$")]
    public string Organization { get; set; } = string.Empty;

    [Required]
    //[Comment("short string version used in URLs")]
    [RegularExpression(@"^[a-zA-Z0-9]+[a-zA-Z0-9-_]*$", ErrorMessage = "Allowed: digits, letters, dash, and underline.")]
    [JsonProperty(PropertyName = "id")]
    public string ID { get; set; }

    [Required]
    [RegularExpression(@"^[a-zA-Z0-9]+[ a-zA-Z0-9-_]*$", ErrorMessage = "Allowed: digits, letters, dash, underline, and spaces.")]
    public string League { get; set; }

    [Required]
    [RegularExpression(@"^[a-zA-Z0-9]+[ a-zA-Z0-9-_]*$", ErrorMessage = "Allowed: digits, letters, dash, underline, and spaces.")]
    public string Div { get; set; }

    [DisplayFormat(DataFormatString = "{0:MM/dd/yyyy h:mm tt}", ApplyFormatInEditMode = false)]
    public DateTime Updated { get; set; }

    //[Comment("Locked means that scores can no longer be reported")]
    public bool Locked { get; set; }
}

public class DivisionInfoList
{
    [RegularExpression(@"^[a-zA-Z0-9]+[ a-zA-Z0-9-_]*$")]
    public string Organization { get; set; } = string.Empty;

    [JsonProperty(PropertyName = "id")]
    public string ID { get; set; }

    public List<DivisionInfo> DivisionList { get; set; }// = new List<DivisionInfo>();
}


public class Division
{
    [RegularExpression(@"^[a-zA-Z0-9]+[ a-zA-Z0-9-_]*$")] 
    public string Organization { get; set; } = string.Empty;

    [Required]
    //[Comment("short string version used in URLs")]
    [RegularExpression(@"^[a-zA-Z0-9]+[a-zA-Z0-9-_]*$", ErrorMessage = "Allowed: digits, letters, dash, and underline.")]
    [JsonProperty(PropertyName = "id")]
    public string ID { get; set; } // note - all-lowercase here

    public List<Standings> Standings { get; set; } = new List<Standings>();

    public List<Schedule> Schedule { get; set; } = new List<Schedule>();
}

