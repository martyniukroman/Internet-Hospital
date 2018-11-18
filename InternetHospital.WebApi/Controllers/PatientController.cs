﻿using System.Threading.Tasks;
using InternetHospital.BusinessLogic.Interfaces;
using InternetHospital.BusinessLogic.Models;
using InternetHospital.DataAccess.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace InternetHospital.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PatientController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly IPatientService _patientService;
        private readonly IFilesService _uploadingFiles;

        public PatientController(UserManager<User> userManager, IPatientService patientService, IFilesService uploadingFiles)
        {
            _userManager = userManager;
            _uploadingFiles = uploadingFiles;
            _patientService = patientService;
        }

        [HttpPut("updateAvatar")]
        [Authorize]
        public async Task<IActionResult> UpdateAvatar([FromForm(Name = "Image")]IFormFile file)
        {
            var patientId = User.Identity?.Name;
            if (patientId != null && file != null)
            {
                var patient = await _userManager.FindByIdAsync(patientId);                
                await _uploadingFiles.UploadAvatar(file, patient);
                return Ok();
            }
            return BadRequest(new { message = "Cannot change avatar!" });
        }

        [HttpGet("getAvatar")]
        [Authorize]
        public async Task<IActionResult> GetAvatar()
        {
            var patientId = User.Identity?.Name;
            if (patientId != null)
            {
                var patient = await _userManager.FindByIdAsync(patientId);
                return Ok(new
                {
                    patient.AvatarURL
                });
            }
            else
            {
                return BadRequest();
            }
        }

        [HttpPut("update")]
        [Authorize]
        public async Task<IActionResult> UpdatePatientProfile([FromForm(Name = "PassportURL")]IFormFileCollection files)
        {
            //if (files == null)
            //{
            //    ModelState.AddModelError("Files", "Files didn't upload");
            //}
            var patientModel = new PatientModel
            {
                FirstName = Request.Form["FirstName"],
                SecondName = Request.Form["SecondName"], // First letter to Uper other tolower case !!!
                ThirdName = Request.Form["ThirdName"],
                PhoneNumber = Request.Form["PhoneNumber"],
                BirthDate = Request.Form["BirthDate"]
            };

            TryValidateModel(patientModel);

            if (ModelState.IsValid)
            {
                if (int.TryParse(User.Identity.Name, out int userId))
                {
                    var result = await _patientService.UpdatePatientInfo(patientModel, userId, files);
                    return result ? (IActionResult)Ok() : BadRequest(new { message = "Error during updating!" });
                }
                else
                {
                    return BadRequest(new { message = "Error with user ID!" });
                }
            }
            return BadRequest();
        }

        [HttpGet("getProfile")]
        [Authorize]
        public async Task<IActionResult> GetPatientProfile()
        {
            var patientId = User.Identity?.Name;
            if (!int.TryParse(User.Identity.Name, out int userId))
            {
                return BadRequest();
            }

            var patient = await _patientService.GetPatientProfile(userId);
            if (patient != null)
            {
                return Ok(patient);

            }
            return BadRequest(new { message = "Cannot get profile data!"});
        }


    }
}