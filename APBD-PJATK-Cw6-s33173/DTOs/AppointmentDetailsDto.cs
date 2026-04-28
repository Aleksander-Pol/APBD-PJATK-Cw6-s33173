namespace APBD_PJATK_Cw6_s33173.DTOs;

public class AppointmentDetailsDto
{
    
    public string email { get; set; } = string.Empty;
    public string phoneNumber  { get; set; } = string.Empty;
    public string licenseNumber { get; set; } = string.Empty;
    public string? internalNotes { get; set; } = string.Empty;
    public DateTime createdAt { get; set; } 
}