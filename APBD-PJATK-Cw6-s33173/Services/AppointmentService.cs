using APBD_PJATK_Cw6_s33173.DTOs;
using APBD_PJATK_Cw6_s33173.Exceptions;
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

    public async Task<AppointmentDetailsDto> GetAppointmentById(int id)
    {
        AppointmentDetailsDto? appointment = null;

        await using var connection = new SqlConnection(configuration.GetConnectionString("Default"));
        await using var command = new SqlCommand();
        
        command.Connection = connection;

        command.CommandText = """
                              SELECT p.email, p.phoneNumber, d.licenseNumber, a.internalNotes, a.createdAt FROM Appointments a
                              LEFT JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
                              LEFT JOIN dbo.Doctors d ON d.IdDoctor = a.IdDoctor
                              WHERE a.IdAppointment = @id
                              """;
        
        command.Parameters.AddWithValue("@id", id);
        
        await connection.OpenAsync();
        
        var reader =  await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            appointment ??= new AppointmentDetailsDto()
            {
                email = reader.GetString(0),
                phoneNumber = reader.GetString(1),
                licenseNumber = reader.GetString(2),
                internalNotes = reader.IsDBNull(3)? null : reader.GetString(3),
                createdAt = reader.GetDateTime(4),
            };
        }
        
        command.Parameters.Clear();

        if (appointment is null)
        {
            throw new NotFoundException($"Appointment with id {id} not found");
        }
        
        return appointment;
    }

    public async Task<int> CreateAppointmentAsync(CreateAppointmentRequestDto appointment, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(configuration.GetConnectionString("Default"));
        await using var command = new SqlCommand();

        await connection.OpenAsync(cancellationToken);
        
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        command.Connection = connection;
        command.Transaction = (SqlTransaction)transaction;

        command.CommandText = """
                              select 1 from dbo.Patients
                              where idPatient = @PatientId AND IsActive = 1
                              """;

        command.Parameters.AddWithValue("@PatientId", appointment.IdPatient);

        var isPatientRealAndActive = await command.ExecuteScalarAsync(cancellationToken);
        command.Parameters.Clear();

        if (isPatientRealAndActive is null)
            throw new NotFoundException($"Patient of id {appointment.IdPatient} was either not found" +
                                        $"or was not active");

        command.CommandText = """
                              select 1 from dbo.Doctors
                              where idDoctor = @DoctorId AND IsActive = 1
                              """;
        
        command.Parameters.AddWithValue("@DoctorId", appointment.IdDoctor);
        
        var isDoctorRealAndActive = await command.ExecuteScalarAsync(cancellationToken);
        command.Parameters.Clear();
        
        if (isDoctorRealAndActive is null)
            throw new NotFoundException($"Doctor of id {appointment.IdDoctor} was either not found" +
                                        $"or was not active");

        if (appointment.AppointmentDate < DateTime.Now)
            throw new Conflict($"Appointment date: {appointment.AppointmentDate} is in the past");


        command.CommandText = """
                              select 1 from dbo.Appointments a
                              where a.IdDoctor = @DoctorId AND a.AppointmentDate = @AppointmentDate
                              """;

        command.Parameters.AddWithValue("@AppointmentDate", appointment.AppointmentDate);
        command.Parameters.AddWithValue("@DoctorId", appointment.IdDoctor);
        

        var isThisDateFree = await command.ExecuteScalarAsync(cancellationToken);
        
        
        if (isThisDateFree is not null)
            throw new Conflict("This date is already scheduled");
        
        command.Parameters.Clear();

        try
        {
            command.CommandText = """
                                  insert into dbo.Appointments (IdPatient, IdDoctor, AppointmentDate, Status, Reason)
                                  output inserted.IdAppointment
                                  values (@1, @2, @3, @4, @5)
                                  """;

            command.Parameters.AddWithValue("@1", appointment.IdPatient);
            command.Parameters.AddWithValue("@2", appointment.IdDoctor);
            command.Parameters.AddWithValue("@3", appointment.AppointmentDate);
            command.Parameters.AddWithValue("@4", "Scheduled");
            command.Parameters.AddWithValue("@5", appointment.Reason);

            var appointmentId = (int)await command.ExecuteScalarAsync(cancellationToken);
            command.Parameters.Clear();
            
            await transaction.CommitAsync(cancellationToken);

            return appointmentId;

        }
        catch (Exception e)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        
    }
}