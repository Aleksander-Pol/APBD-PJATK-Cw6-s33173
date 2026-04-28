using APBD_PJATK_Cw6_s33173.Services;
using Microsoft.AspNetCore.Mvc;

namespace APBD_PJATK_Cw6_s33173.Controllers;


[ApiController]
[Route("api/[controller]")]
public class AppointmentsController (IAppointmentService appointmentService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery]string? status = null, [FromQuery]string? lastName = null)
    {
        return Ok(await appointmentService.GetAllAppointments(status, lastName));
    }
}