using APBD_PJATK_Cw6_s33173.DTOs;
using APBD_PJATK_Cw6_s33173.Exceptions;
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


    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById([FromRoute]int id)
    {
        try
        {
            return Ok(await appointmentService.GetAppointmentById(id));

        }
        catch (Exception ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody]  CreateAppointmentRequestDto appointment, CancellationToken cancellationToken)
    {

        try
        {
            var newAppointmentId = await appointmentService.CreateAppointmentAsync(appointment, cancellationToken);
            var detailedAppointment = await appointmentService.GetAppointmentById(newAppointmentId);

            return CreatedAtAction(nameof(GetById), new { id = newAppointmentId }, detailedAppointment);

        }
        catch (Exception e)
        {
            return Conflict(e.Message);
        }
        
       
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateAppointmentRequestDto appointment,
        CancellationToken cancellationToken)
    {

        try
        {
            await appointmentService.UpdateAppointmentAsync(id, appointment, cancellationToken);
            return NoContent();
        }
        catch (NotFoundException e)
        {
            return NotFound(e.Message);
        }
        catch (Conflict e)
        {
            return Conflict(e.Message);
        }
        catch (Exception e)
        {
            return BadRequest();
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            await appointmentService.DeleteAppointmentAsync(id, cancellationToken);
            return NoContent();
        }
        catch (NotFoundException e)
        {
            return NotFound(e.Message);
        }
        catch (Conflict e)
        {
            return Conflict(e.Message);
        }
        catch (Exception e )
        {
            return BadRequest(e.Message);
        }
    }
}