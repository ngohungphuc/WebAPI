﻿using JWT_Auth.Data;
using JWT_Auth.Middleware.DataModels;
using JWT_Auth.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace JWT_Auth.Middleware
{
    public class TokenMiddlewareProvider
    {
        private readonly RequestDelegate _next;
        private readonly TokenProviderOptions _options;
        private readonly UserManager<ApplicationUser> _userManager;
        private static ApplicationDbContext db = new ApplicationDbContext();

        public TokenMiddlewareProvider(RequestDelegate next, IOptions<TokenProviderOptions> options, UserManager<ApplicationUser> userManager)
        {
            _next = next;
            _options = options.Value;
            _userManager = userManager;
        }

        public Task Invoke(HttpContext context)
        {
            if (!context.Request.Path.Equals(_options.Path, StringComparison.Ordinal))
            {
                return _next(context);
            }

            if (!context.Request.Path.Equals("POST") || !context.Request.HasFormContentType)
            {
                context.Response.StatusCode = 400;
                return context.Response.WriteAsync("Bad Request");
            }

            return GenerateToken(context);
        }

        public async Task GenerateToken(HttpContext context)
        {
            var username = context.Request.Form["username"];
            var password = context.Request.Form["password"];

            ApplicationUser user = null;
            user = db.Users.Where(x => x.Email == username).FirstOrDefault();

            var result = _userManager.CheckPasswordAsync(user, password);

            if (result.Result == false)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Invalid username or password");
                return;
            }

            var now = DateTime.UtcNow;

            var userClaims = await _userManager.GetRolesAsync(user);
            List<Claim> claims = new List<Claim>();
            claims.Add(new Claim(JwtRegisteredClaimNames.Sub, username));
            claims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));
            claims.Add(new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64));

            foreach (var x in userClaims)
            {
                claims.Add(new Claim(ClaimTypes.Role, x));
            }

            var jwt = new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                notBefore: now,
                expires: now.Add(_options.Expiration),
                signingCredentials: _options.SigningCredentials
            );

            var encodedJwt = new JwtSecurityTokenHandler().WriteToken(jwt);

            var response = new
            {
                access_token = encodedJwt,
                expires_in = (int)_options.Expiration.TotalSeconds
            };

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonConvert.SerializeObject(response, new JsonSerializerSettings { Formatting = Formatting.Indented }));
        }
    }
}