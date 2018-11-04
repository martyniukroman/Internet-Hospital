﻿using InternetHospital.BusinessLogic.Interfaces;
using InternetHospital.BusinessLogic.Models;
using InternetHospital.DataAccess;
using InternetHospital.DataAccess.Entities;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System;

namespace InternetHospital.BusinessLogic.Services
{
    public class DoctorService : IDoctorService
    {
        private ApplicationContext _context;

        public DoctorService(ApplicationContext context)
        {
            _context = context;
        }

        public DoctorDetailedModel Get(int id)
        {
            DoctorDetailedModel returnedModel = null;
            DateTime dt = DateTime.Now;
            var serchedDoctor = _context.Doctors.Include(d => d.User).Include(d => d.Specialization).Include(d => d.Diplomas)
                .FirstOrDefault(d => d.UserId == id && d.User != null);
            var firstVar = DateTime.Now - dt;
            dt = DateTime.Now;
            if (serchedDoctor != null)
            {
                returnedModel = new DoctorDetailedModel
                {
                    Id = serchedDoctor.UserId,
                    FirstName = serchedDoctor.User.FirstName,
                    SecondName = serchedDoctor.User.SecondName,
                    ThirdName = serchedDoctor.User.ThirdName,
                    BirthDate = serchedDoctor.User.BirthDate,
                    Address = serchedDoctor.Address,
                    Specialization = serchedDoctor.Specialization.Name,
                    DoctorsInfo = serchedDoctor.DoctorsInfo,
                    AvatarURL = serchedDoctor.User.AvatarURL,
                    LicenseURL = serchedDoctor.LicenseURL,
                    DiplomaURL = serchedDoctor.Diplomas.Where(d => d.IsValid == true).Select(d => d.DiplomaURL).ToArray(),
                };
            }
            return returnedModel;
        }

        public (IQueryable<DoctorModel> doctors, int count) GetAll(DoctorSearchParameters queryParameters)
        {
            var _doctors = _context.Doctors.AsQueryable();

            if (queryParameters.SearchByName != null)
            {
                var toLowerSearchParameter = queryParameters.SearchByName.ToLower();
                _doctors = _doctors
                    .Where(x => x.User.FirstName.ToLower().Contains(toLowerSearchParameter)
                    || x.User.SecondName.ToLower().Contains(toLowerSearchParameter)
                    || x.User.ThirdName.ToLower().Contains(toLowerSearchParameter));
            }

            if (queryParameters.SearchBySpecialization != null)
            {
                _doctors = _doctors.Where(x => x.SpecializationId == queryParameters.SearchBySpecialization);
            }

            int doctorsAmount = _doctors.Count();
            var doctorsResult = PaginationHelper(_doctors, queryParameters.PageCount, queryParameters.Page);

            return (doctorsResult, doctorsAmount);
        }

        private IQueryable<DoctorModel> PaginationHelper(IQueryable<Doctor> doctors, int pageCount, int page)
        {
            var doctorsModel = doctors
                .Skip(pageCount * (page - 1))
                .Take(pageCount).Select(x => new DoctorModel
                {
                    Id = x.UserId,
                    FirstName = x.User.FirstName,
                    SecondName = x.User.SecondName,
                    ThirdName = x.User.ThirdName,
                    AvatarURL = x.User.AvatarURL,
                    Specialization = x.Specialization.Name
                });

            return doctorsModel;
        }

    }
}
