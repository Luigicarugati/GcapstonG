﻿using GCapstoneBE.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.IO;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Win32.SafeHandles;

namespace GCapstoneBE.Controllers
{
    [RoutePrefix("api/user")]
    public class UserController : ApiController
    {
        BarEntities db = new BarEntities();
        Response response = new Response();

        [HttpPost, Route("singup")]
        public HttpResponseMessage Singup([FromBody] User user)
        {
            try
            {
                User userObj = db.Users
                .Where(u => u.email == user.email).FirstOrDefault();
                if (userObj == null)
                {
                    user.role = "user";
                    user.status = "false";
                    db.Users.Add(user);
                    db.SaveChanges();
                    return Request.CreateResponse(HttpStatusCode.OK, new { message = "Registrazione avvenuta con successo " });
                }
                else
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { message = "Email registrata in precedenza" });
                }
            }
            catch (Exception e)
            {

                return Request.CreateResponse(HttpStatusCode.InternalServerError, e);


            }

        }

        [HttpPost, Route("login")]
        public HttpResponseMessage login([FromBody] User user)
        {
            try
            {
                User userObj = db.Users
                .Where(u => (u.email == user.email && u.password == user.password)).FirstOrDefault();
                if (userObj != null)
                {
                    if (userObj.status == "true")
                    {
                        return Request.CreateResponse(HttpStatusCode.OK, new { token = TokenManager.GenerateToken(userObj.email, userObj.role) });
                    }
                    else
                    {
                        return Request.CreateResponse(HttpStatusCode.Unauthorized, new { message = " Attesa approvazione da Admin" });
                    }
                }

                else

                {
                    return Request.CreateResponse(HttpStatusCode.Unauthorized, new { message = "User o Password non corretti" });
                }


            }
            catch (Exception e)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, e);

            }
        }

        [HttpGet, Route("checkToken")]
        [CustomAuthenticationFilter]
        public HttpResponseMessage checkToken()
        {
            return Request.CreateResponse(HttpStatusCode.OK, new { message = "true" });

        }

        [HttpGet, Route("getAllUser")]
        [CustomAuthenticationFilter]
        public HttpResponseMessage GetAllUser()
        {
            try
            {
                var token = Request.Headers.GetValues("authorization").First();
                TokenClaim tokenClaim = TokenManager.ValidateToken(token);
                if (tokenClaim.Role != "admin")
                {
                    return Request.CreateResponse(HttpStatusCode.Unauthorized);
                }
                var result = db.Users
                    .Select(u => new { u.id, u.name, u.contactNumber, u.email, u.status, u.role })
                    .Where(x => (x.role == "user"))
                    .ToList();
                return Request.CreateResponse(HttpStatusCode.OK, result);

            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex);
            }
        }


        [HttpPost, Route("updateUserStatus")]
        [CustomAuthenticationFilter]

        public HttpResponseMessage UpdateUserStatus(User user)
        {
            try
            {
                var token = Request.Headers.GetValues("authorization").First();
                TokenClaim tokenClaim = TokenManager.ValidateToken(token);
                if (tokenClaim.Role != "admin")
                {
                    return Request.CreateResponse(HttpStatusCode.Unauthorized);
                }
                User userObj = db.Users.Find(user.id);
                if (userObj == null)
                {
                    response.message = ("Utente non trovato");
                    return Request.CreateResponse(HttpStatusCode.OK, response);
                }
                userObj.status = user.status;
                db.Entry(userObj).State = System.Data.Entity.EntityState.Modified;
                db.SaveChanges();
                response.message = "User approvato da Admin";
                return Request.CreateResponse(HttpStatusCode.OK, response);
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        [HttpPost, Route("changePassword")]
        [CustomAuthenticationFilter]

        public HttpResponseMessage ChangePassword(ChangePassword changePassword)
        {
            try
            {
                var token = Request.Headers.GetValues("authorization").First();
                TokenClaim tokenClaim = TokenManager.ValidateToken(token);

                User userObj = db.Users
                    .Where(x => x.email == tokenClaim.Email && x.password == changePassword.OldPassword).FirstOrDefault();
                if (userObj != null)
                {
                    userObj.password = changePassword.NewPassword;
                    db.Entry(userObj).State = System.Data.Entity.EntityState.Modified;
                    db.SaveChanges();
                    response.message = "Password cambiata con successo ";
                    return Request.CreateResponse(HttpStatusCode.OK, response);
                }
                else
                {
                    response.message = "Vecchia password errata";
                    return Request.CreateResponse(HttpStatusCode.BadRequest, response);
                }
            }

            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        private string createEmailBody(string email,string password)
        {
            try
            {
                string body = string.Empty;
                using (StreamReader reader = new StreamReader(HttpContext.Current.Server.MapPath("/Template/forgot-password.html")))
                {
                    body = reader.ReadToEnd();
                }
                body = body.Replace("email", email);
                body = body.Replace("password", password);
                body = body.Replace("frontendUrl", "http:localhost:4200/");
                return body;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null;
            }
        }

        [HttpPost, Route("forgotPassword")]
        public async Task<HttpResponseMessage> ForgotPassword([FromBody] User user) {
            User userObj = db.Users
                .Where(x => x.email == user.email).FirstOrDefault();
            response.message = "Password inviata alla tua mail";
            if (userObj == null)
            {
                return Request.CreateResponse(HttpStatusCode.OK, response);
            }
            var message = new MailMessage();
            message.To.Add(new MailAddress(user.email));
            message.Subject = "Password da Gestionale per Bar";
            message.Body = createEmailBody(user.email,userObj.password);
            message.IsBodyHtml = true;
            using (var smtp = new SmtpClient())
            {
                await smtp.SendMailAsync(message);
                await Task.FromResult(0);
            }
            return Request.CreateResponse(HttpStatusCode.OK, response);
        }
    }
}
