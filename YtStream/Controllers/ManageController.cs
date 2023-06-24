using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using YtStream.Enums;
using YtStream.Extensions;
using YtStream.Models;
using YtStream.Models.Accounts;
using YtStream.Services;
using YtStream.Services.Accounts;
using YtStream.Services.Mp3;

namespace YtStream.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class ManageController : BaseController
    {
        private readonly ILogger _logger;
        private readonly string _basePath;
        private readonly BrotliService _brotli;
        private readonly CacheService _cache;
        private readonly ApplicationLockService _lock;
        private readonly ConfigService _config;
        private readonly StreamKeyLockService _streamLock;

        public ManageController(ILogger<StreamController> Logger, ConfigService config,
            UserManagerService userManager,
            BasePathService basePath, BrotliService brotli,
            CacheService cache, ApplicationLockService lockService,
            StreamKeyLockService streakLock) : base(config, userManager)
        {
            _logger = Logger;
            _basePath = basePath.BasePath;
            _brotli = brotli;
            _cache = cache;
            _lock = lockService;
            _config = config;
            _streamLock = streakLock;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        #region Cache

        [HttpGet]
        public IActionResult CacheInfo()
        {
            RequireSettings();
            return View(_cache.GetHandler(CacheTypeEnum.MP3, Settings.CacheMp3Lifetime));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult CacheClean()
        {
            RequireSettings();
            var Handler = _cache.GetHandler(CacheTypeEnum.MP3, Settings.CacheMp3Lifetime);
            try
            {
                var Msg = $"Deleted {Handler.ClearStale()} expired files from cache";
                return RedirectWithMessage("CacheInfo", Msg, true);
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel(ex));
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult CachePurge()
        {
            RequireSettings();
            try
            {
                Tools.CheckFormConfirmation(Request.Form);
                var Handler = _cache.GetHandler(CacheTypeEnum.MP3, Settings.CacheMp3Lifetime);
                var Count = Handler.Purge();
                _logger.LogInformation("MP3 cache purge: Deleted {count} files", Count);
                return RedirectWithMessage("CacheInfo", $"Deleted {Count} files from cache", true);
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel(ex));
            }
        }

        #endregion

        #region Config Backup

        [HttpGet]
        public async Task<IActionResult> Backup(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                ViewBag.Msg = CookieMessage;
                return View();
            }
            string Filename, Fakename;
            try
            {
                switch (id)
                {
                    case "accounts":
                        Filename = System.IO.Path.Combine(_basePath, UserManagerService.FileName);
                        Fakename = "backup.ytacc";
                        break;
                    case "config":
                        Filename = System.IO.Path.Combine(_basePath, ConfigService.ConfigFileName);
                        Fakename = "backup.ytconf";
                        break;
                    default:
                        throw new ArgumentException("Unknown backup type");
                }
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel(ex));
            }
            if (!string.IsNullOrEmpty(Filename))
            {
                using (var FS = System.IO.File.OpenRead(Filename))
                {
                    byte[] Result = await _brotli.Compress(FS);
                    HttpContext.Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{Fakename}\"");
                    return File(Result, "application/octet-stream");
                }
            }
            return NotFound();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(string id)
        {
            try
            {
                if (Request.Form.Files.Count != 1)
                {
                    throw new Exception($"Expected 1 file, but got {Request.Form.Files.Count}");
                }
                if (id == null)
                {
                    throw new Exception("No upload type specified");
                }
                switch (id)
                {
                    case "accounts":
                        return await RestoreAccounts(Request.Form.Files[0]);
                    case "config":
                        return await RestoreConfig(Request.Form.Files[0]);
                    default:
                        throw new Exception("Unknown upload type");
                }
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel(ex));
            }
        }

        #endregion

        #region Accounts

        [HttpGet]
        public IActionResult AccountList()
        {
            return View(_userManager.GetUsers());
        }

        [HttpGet, ActionName("AccountDelete")]
        public IActionResult AccountDeleteGet(string id)
        {
            try
            {
                return View(GetAccount(id));
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel(ex));
            }
        }

        [HttpPost, ActionName("AccountDelete"), ValidateAntiForgeryToken]
        public IActionResult AccountDeletePost(string id)
        {
            AccountInfoModel Acc;
            try
            {
                //If we do not allow the user to delete itself we guarantee that at least one administrator is remaining.
                Tools.CheckFormConfirmation(Request.Form);
                Acc = GetAccount(id);
                _userManager.DeleteUser(id);
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel(ex));
            }
            _logger.LogInformation("User {username} deleted", Acc.Username);
            return RedirectWithMessage("AccountList", $"User '{Acc.Username}' was deleted", true);
        }

        [HttpGet]
        public IActionResult AccountEdit(string id)
        {
            AccountInfoModel Acc;
            try
            {
                Acc = GetAccount(id);
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel(ex));
            }
            return View(Acc);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult AccountRename(string id, string username)
        {
            AccountInfoModel Acc;
            if (id != null && username != null)
            {
                if (id == username)
                {
                    return RedirectWithMessage("AccountEdit", "No change", false, new { id = username });
                }
                try
                {
                    Acc = GetAccount(id);
                    if (id.ToLower() == username.ToLower())
                    {
                        //Just an upper/lowercase change.
                        //This bypasses validation because user names are case-insensitive
                        //and thus validation would always fail
                        Acc.Username = username;
                        _userManager.Save();
                        return RedirectWithMessage("AccountEdit", $"Renamed to {username}", true, new { id = username });
                    }
                    //Regular renaming action
                    if (_userManager.GetUser(username) != null)
                    {
                        throw new InvalidOperationException("An account with this name already exists");
                    }
                    if (!_userManager.IsValidUsername(username, false))
                    {
                        throw new FormatException("Invalid user name");
                    }
                    Acc.Username = username;
                    _userManager.Save();
                    return RedirectWithMessage("AccountEdit", $"Renamed to {username}", true, new { id = username });
                }
                catch (Exception ex)
                {
                    return View("Error", new ErrorViewModel(ex));
                }
            }
            return BadRequest();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult AccountAdd(string userName, string password, string passwordRepeat)
        {
            try
            {
                if (password == passwordRepeat)
                {
                    AccountInfoModel NewUser = _userManager.AddUser(userName, password, UserRoles.User);
                    _logger.LogInformation("User {newname} registered by {admin}", NewUser.Username, CurrentUser?.Username);
                }
                else
                {
                    throw new ArgumentException("Passwords do not match");
                }
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel(ex));
            }
            return RedirectToAction("AccountEdit", new { id = userName });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult AccountDisable(string Username)
        {
            return ChangeUserEnabled(Username, false);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult AccountEnable(string Username)
        {
            return ChangeUserEnabled(Username, true);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult AccountPasswordChange(string id, string password, string passwordRepeat)
        {
            AccountInfoModel Acc;
            var PI = new UserPasswordRuleModel();
            try
            {
                Acc = GetAccount(id);
                if (!PI.IsComplexPassword(password))
                {
                    throw new FormatException("Supplied password is not complex enough");
                }
                if (password != passwordRepeat)
                {
                    throw new ArgumentException("passwords do not match");
                }
                Acc.SetPassword(password);
                _userManager.Save();
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel(ex));
            }
            return RedirectWithMessage("AccountEdit", "Password changed", true, new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult AccountPermission(string id)
        {
            AccountInfoModel? acc;
            UserRoles role = 0;
            try
            {
                if (!HttpContext.Request.HasFormContentType)
                {
                    return BadRequest();
                }
                acc = GetAccount(id);
                if (acc == null)
                {
                    return BadRequest();
                }
                var roles = HttpContext.Request.Form["permission"];
                if (roles.Count == 0)
                {
                    throw new InvalidOperationException("At least one role must be set");
                }
                foreach (var str in roles)
                {
                    var Parsed = (UserRoles)Enum.Parse(typeof(UserRoles), str!);
                    role |= Parsed;
                    if (Parsed == UserRoles.Administrator)
                    {
                        role |= UserRoles.User;
                    }
                }
                acc.Roles = role;
                _userManager.Save();
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel(ex));
            }
            return RedirectWithMessage("AccountEdit", "Permissions adjusted", true, new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult AccountSetOptions(string id, bool noads)
        {
            AccountInfoModel Acc;
            try
            {
                if (!HttpContext.Request.HasFormContentType)
                {
                    return BadRequest();
                }
                Acc = GetAccount(id);
                Acc.DisableAds = noads;
                _userManager.Save();
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel(ex));
            }
            return RedirectWithMessage("AccountEdit", "Options adjusted", true, new { id });
        }

        #endregion

        #region Settings

        [HttpPost, ActionName("ChangeLock"), ValidateAntiForgeryToken]
        public IActionResult ChangeLockPost()
        {
            try
            {
                //Locking is always possible, but unlocking requires a valid configuration
                if (_lock.Locked)
                {
                    if (Settings == null || !Settings.IsValid())
                    {
                        throw new InvalidOperationException("Cannot change lock state while the configuration is invalid");
                    }
                }
            }
            catch (Exception ex)
            {
                _lock.Lock();
                _logger.LogError(ex, "Failed to validate settings");
                return View("Error", new ErrorViewModel(ex));
            }
            if (_lock.Locked)
            {
                _lock.Unlock();
            }
            else
            {
                _lock.Lock();
            }
            return RedirectWithMessage("ChangeLock", "Application lock changed", true);
        }

        [HttpGet, ActionName("ChangeLock")]
        public IActionResult ChangeLockGet()
        {
            return View();
        }

        [HttpGet, ActionName("Config")]
        public async Task<IActionResult> ConfigGet()
        {
            if (!string.IsNullOrEmpty(Settings?.FfmpegPath))
            {
                ViewData["FFMpegVersion"] = await Mp3ConverterService.GetVersion(Settings.FfmpegPath);
            }
            else
            {
                ViewData["FFMpegVersion"] = "None";
            }
            if (!string.IsNullOrEmpty(Settings?.YoutubedlPath))
            {
                ViewData["YtDlVersion"] = await YoutubeDlService.GetVersion(Settings.YoutubedlPath);
                ViewData["YtDlUa"] = await YoutubeDlService.GetUserAgent(Settings.YoutubedlPath);
            }
            else
            {
                ViewData["YtDlVersion"] = "None";
                ViewData["YtDlUa"] = "None";
            }

            return View(Settings);
        }

        [HttpPost, ActionName("Config"), ValidateAntiForgeryToken]
        public IActionResult ConfigPost(ConfigModel model)
        {
            ViewBag.ErrorMessage = "";
            if (model == null)
            {
                return RedirectToAction("Config");
            }
            if (!model.IsValid())
            {
                return View(model);
            }
            if (_streamLock.IsAdjustingSettings && _config.GetConfiguration().MaxKeyUsageCount != model.MaxKeyUsageCount)
            {
                SetMessage("The system is still adjusting the streaming keys from a previous settings change. " +
                    "You cannot change this value as of now", false);
                return View(model);
            }
            _config.SaveConfiguration(model);
            _streamLock.UpdateMaxCount();
            return RedirectWithMessage("Config", "Settings saved and applied", true);
        }

        #endregion

        #region Internal helper functions

        private IActionResult ChangeUserEnabled(string Username, bool State)
        {
            AccountInfoModel Acc;
            var Msg = State ? "enabled" : "disabled";
            try
            {
                Acc = GetAccount(Username, false);
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel(ex));
            }
            Acc.Enabled = State;
            _userManager.Save();
            return RedirectWithMessage("AccountList", $"User {Username} was {Msg}", true);
        }

        private AccountInfoModel GetAccount(string Username, bool AllowSelf = false)
        {
            if (string.IsNullOrEmpty(Username))
            {
                throw new ArgumentNullException(nameof(Username), "User name not specified");
            }
            var Acc = _userManager.GetUser(Username) ?? throw new ArgumentException("User not found: " + nameof(Username));
            if (!AllowSelf && Acc == CurrentUser)
            {
                throw new Exception(Username + " is the currently logged on user. " +
                    "To edit the current account, click on the user name in the top right corner.");
            }
            return Acc;
        }

        private async Task<IActionResult> RestoreConfig(IFormFile formFile)
        {
            using (var S = formFile.OpenReadStream())
            {
                var data = (await _brotli.Decompress(S)).Utf8().FromJson<ConfigModel>(true);
                if (!data.IsValid())
                {
                    throw new Exception(string.Join("\r\n", data.GetValidationMessages()));
                }
                _config.SaveConfiguration(data);
            }
            return RedirectWithMessage("Backup", "Settings imported", true);
        }

        private async Task<IActionResult> RestoreAccounts(IFormFile formFile)
        {
            using (var S = formFile.OpenReadStream())
            {
                var data = (await _brotli.Decompress(S)).Utf8().FromJson<AccountInfoModel[]>(true);
                if (data.Length < 1)
                {
                    throw new Exception("Imported user list is empty");
                }
                if (!data.Any(m => m.Enabled && m.Roles.HasFlag(UserRoles.Administrator)))
                {
                    throw new Exception("Imported user list has no enabled administrator");
                }
                var Invalid = data.FirstOrDefault(m => !m.IsValid());
                if (Invalid != null)
                {
                    throw new Exception("Imported user list has invalid entries. " +
                        $"First invalid user is '{Invalid.Username}'. Error: '{Invalid.GetValidationMessages()[0]}'");
                }
                await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(_basePath, UserManagerService.FileName), data.ToJson(true));
                _userManager.Reload();
            }
            return RedirectWithMessage("Backup", "User accounts imported", true);
        }

        #endregion
    }
}
