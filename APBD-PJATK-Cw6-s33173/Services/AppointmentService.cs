using APBD_PJATK_Cw6_s33173.DTOs;
using Microsoft.Data.SqlClient;

namespace APBD_PJATK_Cw6_s33173.Services;

public class AppointmentService (IConfiguration configuration) : IAppointmentService
{
    public async Task<IEnumerable<AppointmentListDto>> GetAllAppointments(String? status = null, String? lastName = null)
    {
        List<AppointmentListDto> result = new List<AppointmentListDto>();


        await using var connection = new SqlConnection(configuration.GetConnectionString("Default"));
       await using var command = new SqlCommand();


        command.Connection = connection;
        
        command.Parameters.AddWithValue("@Status", status).Value = string.IsNullOrWhiteSpace(status)? DBNull.Value : status;
        command.Parameters.AddWithValue("@PatientLastName", lastName).Value = string.IsNullOrWhiteSpace(lastName)? DBNull.Value : lastName;
        
        command.CommandText = """
                              SELECT
                                  a.IdAppointment,
                                  a.AppointmentDate,
                                  a.Status,
                                  a.Reason,
                                  p.FirstName + N' ' + p.LastName AS PatientFullName,
                                  p.Email AS PatientEmail
                              FROM dbo.Appointments a
                              JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
                              WHERE (@Status IS NULL OR a.Status = @Status)
                                AND (@PatientLastName IS NULL OR p.LastName = @PatientLastName)
                              ORDER BY a.AppointmentDate;
                              """;
        
       

        await connection.OpenAsync();

        var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new AppointmentListDto()
            {
                IdAppointment = reader.GetInt32(0),
                AppointmentDate = reader.GetDateTime(1),
                Status = reader.GetString(2),
                Reason = reader.GetString(3),
                PatientFullName = reader.GetString(4),
                PatientEmail = reader.GetString(5), 
            });
        }
        
        command.Parameters.Clear();
        
        return result;
    }
}