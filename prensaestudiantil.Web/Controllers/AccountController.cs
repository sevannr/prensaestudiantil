﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using prensaestudiantil.Web.Data;
using prensaestudiantil.Web.Data.Entities;
using prensaestudiantil.Web.Helpers;
using prensaestudiantil.Web.Models;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace prensaestudiantil.Web.Controllers
{
    // TODO: complete the actios (update, delete) users
    // TODO: actions for edit users
    // TODO: create user with password or send email with token?
    public class AccountController : Controller
    {
        private readonly DataContext _dataContext;
        private readonly IConfiguration _configuration;
        private readonly IUserHelper _userHelper;

        public AccountController(
            DataContext dataContext,
            IConfiguration configuration,
            IUserHelper userHelper)
        {
            _dataContext = dataContext;
            _configuration = configuration;
            _userHelper = userHelper;
        }

        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Index()
        {
            var users = _dataContext.Users.Include(u => u.Publications);

            //TODO: fix user roles, try select
            // Verify  if user is manager (admin)
            foreach (var user in users)
            {
                // TODO: change hardcode roleName
                user.IsManager = await _userHelper.IsInRoleAsync(user.Email, "Manager");
            };

            return View(users);
        }

        //public async Task<IActionResult> ChangeUser()
        //{
        //    var user = await _userHelper.GetUserByEmailAsync(User.Identity.Name);
        //    if (user == null)
        //    {
        //        return NotFound();
        //    }

        //    var view = new EditUserViewModel
        //    {
        //        Address = user.Address,
        //        Document = user.Document,
        //        FirstName = user.FirstName,
        //        LastName = user.LastName,
        //        PhoneNumber = user.PhoneNumber
        //    };

        //    return View(view);
        //}

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> ChangeUser(EditUserViewModel view)
        //{
        //    if (ModelState.IsValid)
        //    {
        //        var user = await _userHelper.GetUserByEmailAsync(User.Identity.Name);

        //        user.Document = view.Document;
        //        user.FirstName = view.FirstName;
        //        user.LastName = view.LastName;
        //        user.Address = view.Address;
        //        user.PhoneNumber = view.PhoneNumber;

        //        await _userHelper.UpdateUserAsync(user);
        //        return RedirectToAction("Index", "Home");
        //    }

        //    return View(view);
        //}

        [Authorize(Roles = "Manager")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AddUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await AddUser(model);
                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "This email is already used.");
                    return View(model);
                }

                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        // TODO move method to API
        [HttpPost]
        public async Task<IActionResult> CreateToken([FromBody] LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userHelper.GetUserByEmailAsync(model.Username);
                if (user != null)
                {
                    var result = await _userHelper.ValidatePasswordAsync(
                        user,
                        model.Password);

                    if (result.Succeeded)
                    {
                        var claims = new[]
                        {
                            new Claim(JwtRegisteredClaimNames.Sub, user.Email),
                            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                        };

                        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Tokens:Key"]));
                        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
                        var token = new JwtSecurityToken(
                            _configuration["Tokens:Issuer"],
                            _configuration["Tokens:Audience"],
                            claims,
                            expires: DateTime.UtcNow.AddMonths(4),
                            signingCredentials: credentials);
                        var results = new
                        {
                            token = new JwtSecurityTokenHandler().WriteToken(token),
                            expiration = token.ValidTo
                        };

                        return Created(string.Empty, results);
                    }
                }
            }

            return BadRequest();
        }

        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userHelper.GetUserByIdAsync(id);

            // TODO: fix the responses: Normalize to <object> => Success, etc
            await _userHelper.DeleteUserAsync(user);

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _dataContext.Users
                .Include(p => p.Publications)
                .ThenInclude(p => p.PublicationCategory)
                .Include(p => p.Publications)
                .ThenInclude(p => p.PublicationImages)
                .Include(u => u.YoutubeVideos)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound();
            }

            user.Roles = await _userHelper.GetRolesAsync(user.Email);

            return View(user);
        }

        //public async Task<IActionResult> Edit(string id)
        //{
        //    if (string.IsNullOrEmpty(id))
        //    {
        //        return NotFound();
        //    }

        //    var user = await _dataContext.Users
        //        .FirstOrDefaultAsync(u => u.Id == id);
            
        //    if (owner == null)
        //    {
        //        return NotFound();
        //    }

        //    var view = new EditUserViewModel
        //    {
        //        Address = owner.User.Address,
        //        Document = owner.User.Document,
        //        FirstName = owner.User.FirstName,
        //        Id = owner.Id,
        //        LastName = owner.User.LastName,
        //        PhoneNumber = owner.User.PhoneNumber
        //    };

        //    return View(view);
        //}

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Edit(EditUserViewModel view)
        //{
        //    if (ModelState.IsValid)
        //    {
        //        var owner = await _dataContext.Owners
        //            .Include(o => o.User)
        //            .FirstOrDefaultAsync(o => o.Id == view.Id);

        //        owner.User.Document = view.Document;
        //        owner.User.FirstName = view.FirstName;
        //        owner.User.LastName = view.LastName;
        //        owner.User.Address = view.Address;
        //        owner.User.PhoneNumber = view.PhoneNumber;

        //        await _userHelper.UpdateUserAsync(owner.User);
        //        return RedirectToAction(nameof(Index));
        //    }

        //    return View(view);
        //}
                
        public IActionResult Login()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                Microsoft.AspNetCore.Identity.SignInResult result = await _userHelper.LoginAsync(model);
                if (result.Succeeded)
                {
                    if (Request.Query.Keys.Contains("ReturnUrl"))
                    {
                        return Redirect(Request.Query["ReturnUrl"].First());
                    }

                    return RedirectToAction("Index", "Home");
                }
            }

            ModelState.AddModelError(string.Empty, "Email or Password incorrect :(");
            return View(model);
        }

        public async Task<IActionResult> Logout()
        {
            await _userHelper.LogoutAsync();
            return RedirectToAction("Index", "Home");
        }

        public IActionResult NotAuthorized()
        {
            return View();
        }

        [Authorize(Roles = "Manager")]
        private async Task<User> AddUser(AddUserViewModel model)
        {
            var user = new User
            {
                Email = model.Username,
                FirstName = model.FirstName,
                IsEnabled = true,
                LastName = model.LastName,
                PhoneNumber = model.PhoneNumber,
                UserName = model.Username
            };

            var result = await _userHelper.AddUserAsync(user, model.Password);
            if (result != IdentityResult.Success)
            {
                return null;
            }

            var newUser = await _userHelper.GetUserByEmailAsync(model.Username);
            //TODO: modify harcode
            await _userHelper.AddUserToRoleAsync(newUser, "Writer");
            return newUser;
        }

        private bool UserExists(string id)
        {
            return _dataContext.Users.Any(u => u.Id == id);
        }
    }
}