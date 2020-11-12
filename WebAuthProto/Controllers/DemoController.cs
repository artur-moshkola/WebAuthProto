using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Text;
using System.Threading.Tasks;
using Fido2NetLib.Objects;
using Fido2NetLib;
using static Fido2NetLib.Fido2;
using Newtonsoft.Json;

namespace WebAuthProto.Controllers
{
    public class DemoApiController : Controller
    {
        private readonly IUserManager _userStorage;

        public DemoApiController(IUserManager userStorage)
        {
            _userStorage = userStorage;
        }

        [HttpPost]
        public ContentResult FakeAuthenticate(string username)
        {
            _userStorage.AuthenticateUser(username);
            return Content("{ \"status\": \"ok\" }", "application/json");
        }

        private class CurrUser
		{
            public CurrUser()
			{
                IsAuth = false;
                User = null;
            }

            public CurrUser(Fido2User user)
			{
                IsAuth = user != null;
                User = user;
            }


            [JsonProperty("isAuth")]
            bool IsAuth { get; set; }
            [JsonProperty("user")]
            Fido2User User { get; set; }
        }

        [HttpGet]
        public async Task<ContentResult> GetCurrentUser()
        {
            var user = await _userStorage.GetCurrentUserAsync();

            var json = JsonConvert.SerializeObject(new CurrUser(user));
            return Content(json, "application/json");
        }

        [HttpGet]
        public ContentResult LogOut()
        {
            _userStorage.LogOut();
            return Content("{ \"status\": \"ok\" }", "application/json");
        }
    }
}