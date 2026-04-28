namespace APBD_PJATK_Cw6_s33173.DTOs;

public class AppointmentDetailsDto
{
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber  { get; set; } = string.Empty;
    public string LicenseNumber { get; set; } = string.Empty;
    public string? InternalNotes { get; set; }
    public DateTime CreatedAt { get; set; } 
}