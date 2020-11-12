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
using System.IO;

namespace WebAuthProto.Controllers
{
    public class Fido2ApiController : Controller
    {
        private const string _userCookieName = "_fido2_registerd_user";

        private readonly IFido2 _fido2;
        private readonly IUserManager _userStorage;
        private readonly ICredentialStorage _credentialStorage;

        public Fido2ApiController(IFido2 fido2, IUserManager userStorage, ICredentialStorage credentialStorage)
        {
            _fido2 = fido2;
            _userStorage = userStorage;
            _credentialStorage = credentialStorage;
        }

        public ActionResult Test()
		{
            return Content("ok", "text/plain");
		}

        private string FormatException(Exception e)
        {
            return string.Format("{0}{1}", e.Message, e.InnerException != null ? " (" + e.InnerException.Message + ")" : "");
        }

        private new ContentResult Json(object data)
		{
            return Content(JsonConvert.SerializeObject(data), "application/json");
		}

        [HttpGet]
        public async Task<ContentResult> MakeCredentialOptions()
        {
            try
            {
                var user = await _userStorage.GetCurrentUserAsync();

                var ex = await _credentialStorage.GetUserCredentialsAsync(user.Id);

                var existingKeys = ex.Select(c => c.Descriptor).ToList();

                var authenticatorSelection = new AuthenticatorSelection
                {
                    RequireResidentKey = false,
                    UserVerification = UserVerificationRequirement.Discouraged
                };

                //authenticatorSelection.AuthenticatorAttachment = authType.ToEnum<AuthenticatorAttachment>();

                var exts = new AuthenticationExtensionsClientInputs()
                {
                    /*Extensions = true,
                    UserVerificationIndex = true,
                    Location = true,
                    UserVerificationMethod = true,
                    BiometricAuthenticatorPerformanceBounds = new AuthenticatorBiometricPerfBounds
                    {
                        FAR = float.MaxValue,
                        FRR = float.MaxValue
                    }*/
                };

                var options = _fido2.RequestNewCredential(user, existingKeys, authenticatorSelection, AttestationConveyancePreference.None, exts);

                HttpContext.Session["fido2.attestationOptions"] = options.ToJson();

                Response.Cookies.Set(new HttpCookie(_userCookieName, user.Name)
                {
                    Expires = DateTime.Now.AddDays(600)
                });

                return Json(options);
            }
            catch (Exception e)
            {
                return Json(new CredentialCreateOptions { Status = "error", ErrorMessage = FormatException(e) });
            }
        }

        [HttpPost]
        public async Task<ContentResult> MakeCredential()
        {
            try
            {
                string body;
                Request.InputStream.Seek(0, SeekOrigin.Begin);
                using (var brdr = new StreamReader(Request.InputStream))
                {
                    body = await brdr.ReadToEndAsync();
                }
                var attestationResponse = JsonConvert.DeserializeObject<AuthenticatorAttestationRawResponse>(body);

                // 1. get the options we sent the client
                var jsonOptions = HttpContext.Session["fido2.attestationOptions"] as String;
                var options = CredentialCreateOptions.FromJson(jsonOptions);

                // 2. Create callback so that lib can verify credential id is unique to this user
                IsCredentialIdUniqueToUserAsyncDelegate callback = (IsCredentialIdUniqueToUserParams args) => Task.FromResult(true);

                // 2. Verify and make the credentials
                var success = await _fido2.MakeNewCredentialAsync(attestationResponse, options, callback);

                // 3. Store the credentials in db
                await _credentialStorage.AddCredentialToUserAsync(options.User.Id, new StoredCredential
                {
                    Descriptor = new PublicKeyCredentialDescriptor(success.Result.CredentialId),
                    PublicKey = success.Result.PublicKey,
                    UserHandle = success.Result.User.Id,
                    SignatureCounter = success.Result.Counter,
                    CredType = success.Result.CredType,
                    RegDate = DateTime.Now,
                    AaGuid = success.Result.Aaguid
                });

                // 4. return "ok" to the client
                return Json(success);
            }
            catch (Exception e)
            {
                return Json(new CredentialMakeResult { Status = "error", ErrorMessage = FormatException(e) });
            }
        }

        [HttpGet]
        public async Task<ActionResult> AssertionOptions()
        {
            try
            {
                var username = Request.Cookies[_userCookieName]?.Value;

                if (String.IsNullOrEmpty(username))
                    throw new Exception("No user registred!");

                var existingCredentials = new List<PublicKeyCredentialDescriptor>();

                if (!string.IsNullOrEmpty(username))
                {
                    // 1. Get user from DB
                    var user = await _userStorage.GetUserByNameAsync(username);
                    if (user == null)
                        throw new ArgumentException("Username was not registered");

                    // 2. Get registered credentials from database
                    var cr = await _credentialStorage.GetUserCredentialsAsync(user.Id);
                    existingCredentials = cr.Select(c => c.Descriptor).ToList();
                }

                var exts = new AuthenticationExtensionsClientInputs()
                {
                    SimpleTransactionAuthorization = "FIDO",
                    GenericTransactionAuthorization = new TxAuthGenericArg
                    {
                        ContentType = "text/plain",
                        Content = new byte[] { 0x46, 0x49, 0x44, 0x4F }
                    },
                    UserVerificationIndex = true,
                    Location = true,
                    UserVerificationMethod = true
                };

                // 3. Create options
                var options = _fido2.GetAssertionOptions(
                    existingCredentials,
                    UserVerificationRequirement.Discouraged,
                    exts
                );

                // 4. Temporarily store options, session/in-memory cache/redis/db
                HttpContext.Session["fido2.assertionOptions"] = options.ToJson();

                // 5. Return options to client
                return Json(options);
            }

            catch (Exception e)
            {
                return Json(new AssertionOptions { Status = "error", ErrorMessage = FormatException(e) });
            }
        }

        [HttpPost]
        public async Task<ContentResult> MakeAssertion()
        {
            try
            {
                string body;
                Request.InputStream.Seek(0, SeekOrigin.Begin);
                using (var brdr = new StreamReader(Request.InputStream))
                {
                    body = await brdr.ReadToEndAsync();
                }
                var clientResponse = JsonConvert.DeserializeObject<AuthenticatorAssertionRawResponse>(body);

                var username = Request.Cookies[_userCookieName]?.Value;

                if (String.IsNullOrEmpty(username))
                    throw new Exception("No user registred!");

                // 1. Get the assertion options we sent the client
                var jsonOptions = HttpContext.Session["fido2.assertionOptions"] as String;
                var options = Fido2NetLib.AssertionOptions.FromJson(jsonOptions);

                // 2. Get registered credential from database
                var user = await _userStorage.GetUserByNameAsync(username);
                var creds = await _credentialStorage.GetCredentialAsync(user.Id, clientResponse.Id);

                if (creds == null)
                {
                    throw new Exception("Unknown credentials");
                }

                // 3. Get credential counter from database
                var storedCounter = creds.SignatureCounter;

                // 4. Create callback to check if userhandle owns the credentialId
                IsUserHandleOwnerOfCredentialIdAsync callback = (args) =>
                {
                    return Task.FromResult(args.UserHandle.SequenceEqual(user.Id));
                };

                // 5. Make the assertion
                var res = await _fido2.MakeAssertionAsync(clientResponse, options, creds.PublicKey, storedCounter, callback);

                // 6. Store the updated counter
                await _credentialStorage.UpdateCounterAsync(user.Id, res.CredentialId, res.Counter);

                _userStorage.AuthenticateUser(user.Name);

                // 7. return OK to client
                return Json(res);
            }
            catch (Exception e)
            {
                return Json(new AssertionVerificationResult { Status = "error", ErrorMessage = FormatException(e) });
            }
        }
    }
}