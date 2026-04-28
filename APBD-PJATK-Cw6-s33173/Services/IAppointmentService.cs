using APBD_PJATK_Cw6_s33173.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace APBD_PJATK_Cw6_s33173.Services;

public interface IAppointmentService
{
    Task<IEnumerable<AppointmentListDto>> GetAllAppointments(String? status = null, String? lastName = null);
    Task<AppointmentDetailsDto> GetAppointmentById(int id);
    Task<int> CreateAppointmentAsync(CreateAppointmentRequestDto appointment, CancellationToken cancellationToken = default);
    Task UpdateAppointmentAsync(int id, UpdateAppointmentRequestDto appointment, CancellationToken cancellationToken = default);
    Task DeleteAppointmentAsync(int id, CancellationToken cancellationToken = default);
}