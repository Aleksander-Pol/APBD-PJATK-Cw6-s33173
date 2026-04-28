namespace APBD_PJATK_Cw6_s33173.DTOs;

public class CreateAppointmentRequestDto
{
    public int IdPatient { get; set; }
    public int IdDoctor { get; set; }
    public DateTime AppointmentDate { get; set; }
    public string? Reason { get; set; }
    
}