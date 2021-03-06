using AutoMapper;
using InternetHospital.BusinessLogic.Interfaces;
using InternetHospital.DataAccess;
using InternetHospital.DataAccess.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using InternetHospital.BusinessLogic.Helpers;
using InternetHospital.BusinessLogic.Models;
using InternetHospital.BusinessLogic.Models.Appointment;
using Microsoft.EntityFrameworkCore;
using System.Transactions;

namespace InternetHospital.BusinessLogic.Services
{
    public class AppointmentService : IAppointmentService
    {
        private readonly ApplicationContext _context;
        private readonly INotificationService _notificationService;

        public AppointmentService(ApplicationContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        /// <summary>
        /// Get all open and reserved appointments for current doctor
        /// </summary>
        /// <param name="doctorId"></param>
        /// <returns>
        /// returns a list of doctor's appointments
        /// </returns>
        public IEnumerable<AppointmentModel> GetMyAppointments(int doctorId)
        {
            DeleteUncommittedAppointments(doctorId);
            var appointments = _context.Appointments
                .Where(a => (a.DoctorId == doctorId)
                            && (a.StatusId == (int)AppointmentStatuses.DEFAULT_STATUS
                                || a.StatusId == (int)AppointmentStatuses.RESERVED_STATUS))
                .Select(a => new AppointmentModel
                {
                    Id = a.Id,
                    UserId = a.UserId,
                    UserFirstName = a.User.FirstName,
                    UserSecondName = a.User.SecondName,
                    Address = a.Address,
                    StartTime = a.StartTime,
                    EndTime = a.EndTime,
                    Status = a.Status.Name
                });
            return appointments.ToList();
        }

        /// <summary>
        /// Get all appointments for current doctor with search params
        /// </summary>
        /// <param name="doctorId"></param>
        /// <returns>
        /// returns a list of doctor's appointments
        /// </returns>
        public PageModel<IEnumerable<AppointmentModel>> GetMyAppointments(AppointmentHistoryParameters parameters, int doctorId)
        {
            DeleteUncommittedAppointments(doctorId);
            var appointments = _context.Appointments
               .OrderByDescending(a => a.StartTime)
               .Include(a => a.IllnessHistory)
               .Include(a => a.User)
               .Include(a => a.Status)
               .Where(a => a.DoctorId == doctorId);

            if (!string.IsNullOrEmpty(parameters.SearchByName))
            {
                var lowerSearchParam = parameters.SearchByName.ToLower();
                appointments = appointments.Where(a => a.User.FirstName.ToLower().Contains(lowerSearchParam)
                                                       || a.User.SecondName.ToLower().Contains(lowerSearchParam));
            }

            if (parameters.From != null)
            {
                appointments = appointments
                    .Where(a => a.StartTime >= parameters.From.Value);
            }

            if (parameters.Till != null)
            {
                appointments = appointments
                    .Where(a => a.StartTime <= parameters.Till.Value);
            }

            if (parameters.Statuses.Count() > 0)
            {
                var predicate = PredicateBuilder.False<Appointment>();

                foreach (var status in parameters.Statuses)
                {
                    predicate = predicate.Or(p => p.StatusId == status);
                }

                appointments = appointments.Where(predicate);
            }

            var appointmentsAmount = appointments.Count();
            var appointmentsResult = PaginationHelper<Appointment>
                .GetPageValues(appointments, parameters.PageCount, parameters.Page)
                .Select(a => Mapper.Map<Appointment, AppointmentModel>(a))
                .ToList();

            return new PageModel<IEnumerable<AppointmentModel>>()
            { EntityAmount = appointmentsAmount, Entities = appointmentsResult };
        }

        public IEnumerable<string> GetAppointmentStatuses()
        {
            var statuses = Enum.GetNames(typeof(AppointmentStatuses))
                .Select(s => (s.Replace('_', ' ')).ToLower());
            return statuses;
        }

        /// <summary>
        /// Get all reserved appointments for current patient
        /// </summary>
        /// <param name="patientId"></param>
        /// <returns>
        /// returns a list of patient's reserved appointments
        /// </returns>
        public IEnumerable<AppointmentForPatient> GetPatientsAppointments(int patientId)
        {
            var appointments = _context.Appointments
                .OrderBy(a => a.StartTime)
                .Where(a => (a.UserId == patientId)
                        && a.StatusId == (int)AppointmentStatuses.RESERVED_STATUS)
                .Select(a => new AppointmentForPatient
                {
                    Id = a.Id,
                    UserId = a.UserId,
                    DoctorFirstName = a.Doctor.User.FirstName,
                    DoctorSecondName = a.Doctor.User.SecondName,
                    DoctorSpecialication = a.Doctor.Specialization.Name,
                    Address = a.Address,
                    StartTime = a.StartTime,
                    EndTime = a.EndTime,
                    Status = a.Status.Name,
                    IsAllowPatientInfo = a.IsAllowPatientInfo
                });
            return appointments.ToList();
        }

        public bool ChangeAccessForPersonalInfo(ChangePatientInfoAccessModel model)
        {
            var appointment = _context.Appointments
               .FirstOrDefault(a => a.Id == model.AppointmentId);

            if (appointment == null)
            {
                return false;
            }
            
            appointment.IsAllowPatientInfo = model.IsAllowPatientInfo;
            try
            {
                _context.SaveChanges();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Create an appointment
        /// </summary>
        /// <param name="creationModel"></param>
        /// <param name="doctorId"></param>
        /// <returns>
        /// returns status of appointment creation
        /// </returns>
        public bool AddAppointment(AppointmentCreationModel creationModel, int doctorId)
        {
            try
            {
                var appointment = Mapper.Map<Appointment>(creationModel);
                appointment.StatusId = (int)AppointmentStatuses.DEFAULT_STATUS;
                appointment.DoctorId = doctorId;
                if (creationModel.Address == null)
                    appointment.Address = _context.Doctors.FirstOrDefault(d => d.UserId == doctorId)?.Address;
                _context.Appointments.Add(appointment);
                _context.SaveChanges();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get all available appointments of selected doctor 
        /// </summary>
        /// <param name="searchParameters"></param>
        /// <returns>
        /// returns a list of appointments what can be reserved by patient
        /// </returns>
        public PageModel<List<AvailableAppointmentModel>>
            GetAvailableAppointments(AppointmentSearchModel searchParameters)
        {
            var appointments = _context.Appointments
                .OrderBy(a => a.StartTime)
                .Where(a => (a.DoctorId == searchParameters.DoctorId)
                            && (a.StatusId == (int)AppointmentStatuses.DEFAULT_STATUS)
                            && (a.StartTime >= searchParameters.From.Value));

            if (searchParameters.Till != null)
            {
                appointments = appointments
                    .Where(a => a.StartTime <= searchParameters.Till.Value);
            }

            var appointmentsAmount = appointments.Count();
            var appointmentsResult = PaginationHelper<Appointment>
                .GetPageValues(appointments, searchParameters.PageCount, searchParameters.Page)
                .Select(a => Mapper.Map<Appointment, AvailableAppointmentModel>(a))
                .ToList();

            return new PageModel<List<AvailableAppointmentModel>>()
            { EntityAmount = appointmentsAmount, Entities = appointmentsResult };
        }

        /// <summary>
        /// Delete doctor's appointment if it's not reserved
        /// </summary>
        /// <param name="appointmentId"></param>
        /// <param name="doctorId"></param>
        /// <returns>
        /// returns status and message about deleting an appointment
        /// </returns>
        public (bool status, string message) DeleteAppointment(int appointmentId, int doctorId)
        {
            var appointment = _context.Appointments
                .FirstOrDefault(a => a.Id == appointmentId);
            if (appointment == null)
            {
                return (false, "Appointment not found");
            }

            if (appointment.DoctorId != doctorId)
            {
                return (false, "You can delete only your appointments");
            }

            if (appointment.StatusId != (int)AppointmentStatuses.DEFAULT_STATUS)
            {
                return (false, "This appointment is reserved. You can only cancel it");
            }

            _context.Appointments.Remove(appointment);
            _context.SaveChanges();

            return (true, "Appointment was deleted");
        }

        /// <summary>
        /// cancel appointment if it was already reserved
        /// </summary>
        /// <param name="appointmentId"></param>
        /// <param name="doctorId"></param>
        /// <returns>
        /// returns status and message of appointment cancellation
        /// </returns>
        public (bool status, string message) CancelAppointment(int appointmentId, int doctorId)
        {
            var appointment = _context.Appointments.Include(a => a.Doctor.User)
                .FirstOrDefault(a => a.Id == appointmentId);
            if (appointment == null)
            {
                return (false, "Appointment not found");
            }

            if (appointment.DoctorId != doctorId)
            {
                return (false, "You can cancel only your appointments");
            }

            if (appointment.StatusId != (int)AppointmentStatuses.RESERVED_STATUS)
            {
                return (false, "You can cancel only reserved appointments");
            }

            try
            {
                using (var scope = new TransactionScope())
                {
                    appointment.StatusId = (int)AppointmentStatuses.CANCELED_STATUS;
                    _context.SaveChanges();

                    _notificationService.AddNotification((int)appointment.UserId, $"Your appointment with {appointment.Doctor.User.FirstName} " +
                        $"{appointment.Doctor.User.SecondName} at {appointment.StartTime.ToShortTimeString()} {appointment.StartTime.ToShortDateString()} " +
                        $"was canceled.");

                    scope.Complete();
                }
                _notificationService.NewMessageNotify((int)appointment.UserId);
                return (true, "Appointment was canceled");
            }
            catch
            {
                return (false, "Error with appointment cancelation");
            }
        }

        /// <summary>
        /// subscribe patient to appointment
        /// </summary>
        /// <param name="appointmentId"></param>
        /// <param name="patientId"></param>
        /// <returns>
        /// status of subscription to appointment
        /// </returns>
        public bool SubscribeForAppointment(AppointmentSubscribeModel appointmentModel, int patientId)
        {
            var appointment = _context.Appointments
                .FirstOrDefault(a => a.Id == appointmentModel.Id);

            if (appointment == null || appointment.StatusId != (int)AppointmentStatuses.DEFAULT_STATUS)
            {
                return false;
            }

            var patient = _context.Users.Find(patientId);
            try
            {
                using (var scope = new TransactionScope())
                {
                    appointment.UserId = patientId;
                    appointment.IsAllowPatientInfo = appointmentModel.IsAllowPatientInfo;
                    appointment.StatusId = (int)AppointmentStatuses.RESERVED_STATUS;
                    _context.SaveChanges();
                    _notificationService.AddNotification(appointment.DoctorId, $"{patient.FirstName} {patient.SecondName} " +
                        $"has subscribed for your appointment at {appointment.StartTime.ToShortTimeString()} " +
                        $"{appointment.StartTime.ToShortDateString()} ");
                    scope.Complete();
                }
                _notificationService.NewMessageNotify(appointment.DoctorId);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// unsubscribe patient to appointment
        /// </summary>
        /// <param name="appointmentId"></param>
        /// <param name="patientId"></param>
        /// <returns>
        /// status of unsubscription to appointment
        /// </returns>
        public bool UnsubscribeForAppointment(int appointmentId)
        {
            var appointment = _context.Appointments
                .FirstOrDefault(a => a.Id == appointmentId);

            if (appointment == null || appointment.StatusId != (int)AppointmentStatuses.RESERVED_STATUS)
            {
                return false;
            }
                        
            var patient = _context.Users.Find(appointment.UserId);
            try
            {
                using (var scope = new TransactionScope())
                {
                    appointment.UserId = null;
                    appointment.StatusId = (int)AppointmentStatuses.DEFAULT_STATUS;
                    _context.SaveChanges();
                    _notificationService.AddNotification(appointment.DoctorId, $"{patient.FirstName} {patient.SecondName} " +
                        $"has unsubscribed for your appointment at {appointment.StartTime.ToShortTimeString()} " +
                        $"{appointment.StartTime.ToShortDateString()} ");
                    scope.Complete();
                }
                _notificationService.NewMessageNotify(appointment.DoctorId);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool DeleteUncommittedAppointments(int doctorId)
        {
            try
            {
                var uncommittedAppointments = _context.Appointments
                    .Where(a => a.DoctorId == doctorId
                                && a.StatusId == (int)AppointmentStatuses.DEFAULT_STATUS
                                && DateTime.Now > a.StartTime);
                _context.RemoveRange(uncommittedAppointments);
                _context.SaveChanges();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}