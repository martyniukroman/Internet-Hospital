﻿using InternetHospital.BusinessLogic.Interfaces;
using InternetHospital.BusinessLogic.Models;
using InternetHospital.DataAccess;
using InternetHospital.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace InternetHospital.BusinessLogic.Services
{
    public class TokenService : ITokenService
    {
        private readonly AppSettings _appSettings;
        private readonly ApplicationContext _context;
        private readonly UserManager<User> _userManager;
        private const string APPROVED = "Approved";
        private const string DOCTOR = "Doctor";
        private const int MAXIMUM_LOGGED_DEVICES = 5;
        private const int REFRESH_TOKEN_LIFETIME = 15;
        public TokenService(ApplicationContext context, IOptions<AppSettings> settings, UserManager<User> userManager)
        {
            _userManager = userManager;
            _appSettings = settings.Value;
            _context = context;
        }

        /// <summary>
        /// Method for generation access tokens for app users
        /// </summary>
        /// <param name="user"></param>
        /// <returns>access token</returns>
        public async Task<JwtSecurityToken> GenerateAccessToken(User user)
        {
            var userRoles = await _userManager.GetRolesAsync(user);
            var userStatus = _context.Statuses.Find(user.StatusId);
            var claims = new List<Claim>
               {
                    new Claim(ClaimTypes.Name, user.Id.ToString()),
                    new Claim(ClaimTypes.Role, userRoles[0])
               };
            if (userStatus.Name == APPROVED)
            {
                claims.Add(new Claim("ApprovedPatient", "approved"));
            }
            if (userRoles[0] == DOCTOR)
            {
                var doctorStatus = _context.Doctors.Find(user.Id);
                if (doctorStatus.IsApproved != null && (bool)doctorStatus.IsApproved)
                    claims.Add(new Claim("ApprovedDoctor", "approved"));
            }
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_appSettings.JwtKey));
            var credential = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var currentTime = DateTime.UtcNow;
            var token = new JwtSecurityToken(
                issuer: _appSettings.JwtIssuer,
                notBefore: currentTime,
                claims: claims,
                expires: currentTime.AddMinutes(_appSettings.JwtExpireMinutes),
                signingCredentials: credential);
            return token;
        }

        /// <summary>
        /// Method for generation and saving in DB refresh tokens for renewing access tokens
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public RefreshToken GenerateRefreshToken(User user)
        {
            var usertokens = _context.Tokens.Where(x => x.UserId == user.Id);
            //you have to relogin if number of logged devices higher than maximum allowed
            if (usertokens.Count() > MAXIMUM_LOGGED_DEVICES)
            {
                foreach (var item in usertokens)
                {
                    _context.Tokens.Remove(item);
                }
                _context.SaveChanges();
            }
            var newRefreshToken = new RefreshToken
            {
                UserId = user.Id,
                Token = Guid.NewGuid().ToString(),
                ExpiresDate = DateTime.UtcNow.AddDays(REFRESH_TOKEN_LIFETIME),
                Revoked = false
            };
            _context.Tokens.Add(newRefreshToken);
            _context.SaveChanges();
            return newRefreshToken;
        }

        /// <summary>
        /// Method for validation of refresh token that was sent by user
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public RefreshToken RefreshTokenValidation(string token)
        {
            RefreshToken refreshedToken = _context.Tokens.FirstOrDefault(x => x.Token == token);
            if (refreshedToken == null)
                return null;
            if (refreshedToken.ExpiresDate < DateTime.UtcNow || refreshedToken.Revoked == true)
            {
                _context.Tokens.Remove(refreshedToken);
                _context.SaveChanges();
                return null;
            }
            string newrefreshtoken = Guid.NewGuid().ToString();
            refreshedToken.Token = newrefreshtoken;
            _context.SaveChanges();
            return refreshedToken;
        }
    }
}
