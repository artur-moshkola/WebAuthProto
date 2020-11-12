using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Fido2NetLib;

namespace WebAuthProto
{
	public interface IUserManager
	{
		Task<Fido2User> GetCurrentUserAsync();
		Task<Fido2User> GetUserByNameAsync(string name);
		void AuthenticateUser(string name);
		void LogOut();
	}

	internal class FakeUserManager : IUserManager
	{
		private const string _sessKey = "fake_auth_user";

		private Fido2User CreateUser(string name)
		{
			return new Fido2User()
			{
				Name = name,
				DisplayName = name,
				Id = Encoding.UTF8.GetBytes(name)
			};
		}

		public void AuthenticateUser(string name)
		{
			HttpContext.Current.Session[_sessKey] = CreateUser(name);
		}

		public Task<Fido2User> GetCurrentUserAsync()
		{
			return Task.FromResult(HttpContext.Current.Session[_sessKey] as Fido2User);
		}

		public Task<Fido2User> GetUserByNameAsync(string name)
		{
			return Task.FromResult(CreateUser(name));
		}

		public void LogOut()
		{
			HttpContext.Current.Session[_sessKey] = null;
		}
	}
}