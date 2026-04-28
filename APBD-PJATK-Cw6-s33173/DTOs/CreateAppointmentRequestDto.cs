using System.ComponentModel.DataAnnotations;

namespace APBD_PJATK_Cw6_s33173.DTOs;

public class CreateAppointmentRequestDto
{
    [Required]
    public int IdPatient { get; set; }
    [Required]
    public int IdDoctor { get; set; }
    
    [Required]
    public DateTime AppointmentDate { get; set; }
    
    [Required(ErrorMessage ="Reason is required")]
    [MaxLength(250)]
    public string Reason { get; set; } = string.Empty;
    
}