using APBD_PJATK_Cw6_s33173.DTOs;
using APBD_PJATK_Cw6_s33173.Exceptions;
using Microsoft.Data.SqlClient;

namespace APBD_PJATK_Cw6_s33173.Services;

public class AppointmentService (IConfiguration configuration) : IAppointmentService
{
    public async Task<IEnumerable<AppointmentListDto>> GetAllAppointments(String? status = null, String? lastName = null)
    {
        List<AppointmentListDto> result = new List<AppointmentListDto>();


        await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
       await using var command = new SqlCommand();


        command.Connection = connection;
        
        command.Parameters.AddWithValue("@Status", string.IsNullOrWhiteSpace(status) ? DBNull.Value : status);
        command.Parameters.AddWithValue("@PatientLastName", string.IsNullOrWhiteSpace(lastName) ? DBNull.Value : lastName);
        
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

        await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
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
                Email = reader.GetString(0),
                PhoneNumber = reader.GetString(1),
                LicenseNumber = reader.GetString(2),
                InternalNotes = reader.IsDBNull(3)? null : reader.GetString(3),
                CreatedAt = reader.GetDateTime(4),
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
        await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
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

    public async Task UpdateAppointmentAsync(int id, UpdateAppointmentRequestDto appointment, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
        await using var command = new SqlCommand();
        
        command.Connection = connection;
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        command.Transaction = (SqlTransaction)transaction;
        
        
        command.CommandText = """
                              select 1 from dbo.Appointments where IdAppointment = @IdAppointment
                              """;
        
        command.Parameters.AddWithValue("@IdAppointment", id);
        var isAppointmentRealAndActive = await command.ExecuteScalarAsync(cancellationToken);
        
        if (isAppointmentRealAndActive is null)
            throw new NotFoundException($"Appointment with id {id} not found");
        
        command.Parameters.Clear();
        
        command.CommandText = """
                              select status, appointmentDate from dbo.Appointments
                              where IdAppointment = @IdAppointment
                              """;

        command.Parameters.AddWithValue("@IdAppointment", id);
        
       await using var reader =  await command.ExecuteReaderAsync(cancellationToken);


       string? currentStatus = null;
       DateTime? currentDate =  null;
       
        while (await reader.ReadAsync(cancellationToken))
        {
            currentStatus = reader.GetString(0);
            currentDate = reader.GetDateTime(1);
        }
        
        await reader.CloseAsync();
        
        command.Parameters.Clear();

        command.CommandText = """
                              select 1 from dbo.Doctors where idDoctor = @DoctorId AND IsActive = 1
                              """;
        
        command.Parameters.AddWithValue("@DoctorId", appointment.IdDoctor);
        var isDoctorRealAndActive = await command.ExecuteScalarAsync(cancellationToken);
        
        if (isDoctorRealAndActive is null)
            throw new Conflict($"Doctor of id {appointment.IdDoctor} was either not found or non active");
        
        command.Parameters.Clear();

        command.CommandText = """
                              select 1 from dbo.Patients where idPatient = @PatientId AND IsActive = 1
                              """;
        
        command.Parameters.AddWithValue("@PatientId", appointment.IdPatient);
        var isPatientRealAndActive = await command.ExecuteScalarAsync(cancellationToken);
        
        if (isPatientRealAndActive is null)
            throw new NotFoundException($"Patient of id {appointment.IdPatient} was either not found");
        
        command.Parameters.Clear();

        if (appointment.Status is not ("Completed" or "Scheduled" or "Cancelled"))
            throw new Conflict($"Appointment status: {appointment.Status} is not supported");

        if (currentStatus == "Completed" && appointment.AppointmentDate != currentDate)
            throw new Conflict($"Appointment status: {appointment.Status} is completed. You cannot change it");
        
        command.CommandText = """
                              select 1 from dbo.Appointments where IdAppointment != @IdAppointment AND AppointmentDate = @AppointmentDate AND @IdDoctor = IdDoctor AND Status = 'Scheduled'
                              """;
        command.Parameters.AddWithValue("@IdAppointment", id);
        command.Parameters.AddWithValue("@AppointmentDate", appointment.AppointmentDate);
        command.Parameters.AddWithValue("@IdDoctor", appointment.IdDoctor);
            
        var appointmentId = await command.ExecuteScalarAsync(cancellationToken);
        command.Parameters.Clear();
            
        if  (appointmentId is not null)
            throw new Conflict($"Appointment with id {id} is colliding with its new Date with some other appointment");


        try
        {
            command.CommandText = """
                                  update Appointments set
                                  IdPatient = @PatientId,
                                  IdDoctor = @DoctorId,
                                  AppointmentDate = @AppointmentDate,
                                  Status = @Status,
                                  Reason = @Reason,
                                  InternalNotes =  @InternalNotes
                                  WHERE IdAppointment = @IdAppointment
                                  """;

            command.Parameters.AddWithValue("@IdAppointment", id);
            command.Parameters.AddWithValue("@DoctorId", appointment.IdDoctor);
            command.Parameters.AddWithValue("@PatientId", appointment.IdPatient);
            command.Parameters.AddWithValue("@AppointmentDate", appointment.AppointmentDate);
            command.Parameters.AddWithValue("@Status", appointment.Status);
            command.Parameters.AddWithValue("@Reason", appointment.Reason);
            command.Parameters.AddWithValue("@InternalNotes", appointment.InternalNotes ?? (object)DBNull.Value);

            await command.ExecuteNonQueryAsync(cancellationToken);
            command.Parameters.Clear();

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        
    }

    public async Task DeleteAppointmentAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
        await using var command = new SqlCommand();

        command.Connection = connection;
        
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        command.Transaction = (SqlTransaction)transaction;

        command.CommandText = """
                              select Status from  dbo.Appointments where  IdAppointment = @IdAppointment
                              """;
        command.Parameters.AddWithValue("@IdAppointment", id);
        
        var result = await command.ExecuteScalarAsync(cancellationToken);
        
        if (result is null)
            throw new NotFoundException($"Appointment with id {id} not found");
        
        if (result.ToString() is "Completed")
            throw new Conflict($"Appointment with id {id} is completed");
        
        command.Parameters.Clear();

        try
        {
            command.CommandText = """
                                  delete from  Appointments where IdAppointment = @IdAppointment
                                  """;

            command.Parameters.AddWithValue("@IdAppointment", id);
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}