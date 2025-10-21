using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace TutoriaApi.Web.API.DTOs;

public class TokenRequest
{
    [Required]
    [FromForm(Name = "grant_type")]
    public string GrantType { get; set; } = string.Empty;

    [Required]
    [FromForm(Name = "client_id")]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    [FromForm(Name = "client_secret")]
    public string ClientSecret { get; set; } = string.Empty;

    [FromForm(Name = "scope")]
    public string? Scope { get; set; }
}
