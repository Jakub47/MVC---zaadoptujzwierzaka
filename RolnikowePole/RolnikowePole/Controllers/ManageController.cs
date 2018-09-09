﻿using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using RolnikowePole.App_Start;
using RolnikowePole.DAL;
using RolnikowePole.Infrastucture;
using RolnikowePole.Models;
using RolnikowePole.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace RolnikowePole.Controllers
{
    [Authorize]
    public class ManageController : Controller
    {
        private RolnikowePoleContext db = new RolnikowePoleContext();

        public enum ManageMessageId
        {
            ChangePasswordSuccess, //Jezeli zmiana hasła była sukcesem 
            Error //jeżeli jest jakiś bląd
        }

        private ApplicationUserManager _userManager;
        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

        private IAuthenticationManager AuthenticationManager
        {
            get
            {
                return HttpContext.GetOwinContext().Authentication;
            }
        }

        // GET: Manage
        public async Task<ActionResult> Index(ManageMessageId? message)
        {
            if (TempData["ViewData"] != null)
            {
                ViewData = (ViewDataDictionary)TempData["ViewData"];
            }

            if (User.IsInRole("Admin"))
                ViewBag.UserIsAdmin = true;
            else
                ViewBag.UserIsAdmin = false;

            var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
            {
                return View("Error");
            }

            var model = new ManageCredentialsViewModel
            {
                Message = message,
                DaneUzytkownika = user.DaneUzytkownika
            };

            return View(model); //Możliwość zmiany
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ChangeProfile([Bind(Prefix = "DaneUzytkownika")]DaneUzytkownika daneUzytkownika)
        {
            if (ModelState.IsValid)
            {
                var user = await UserManager.FindByIdAsync(User.Identity.GetUserId()); //Najpierw bedzie pobierany użytkwonik
                user.DaneUzytkownika = daneUzytkownika; //jego dane zostana pobrane 
                var result = await UserManager.UpdateAsync(user); //a nastepnie uktualnione 

                AddErrors(result);
            }

            if (!ModelState.IsValid)
            {
                TempData["ViewData"] = ViewData;
                return RedirectToAction("Index"); //Zostana przeslane dane ktore beda niepoprawne zwalidowane i przekierowanie do akcji index
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ChangePassword([Bind(Prefix = "ChangePasswordViewModel")]ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ViewData"] = ViewData;
                return RedirectToAction("Index");
            }

            var result = await UserManager.ChangePasswordAsync(User.Identity.GetUserId(), model.OldPassword, model.NewPassword);
            if (result.Succeeded)
            {
                var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
                if (user != null)
                {
                    await SignInAsync(user, isPersistent: false);
                }
                return RedirectToAction("Index", new { Message = ManageMessageId.ChangePasswordSuccess });
            }
            AddErrors(result);

            if (!ModelState.IsValid)
            {
                TempData["ViewData"] = ViewData;
                return RedirectToAction("Index");
            }

            var message = ManageMessageId.ChangePasswordSuccess;
            return RedirectToAction("Index", new { Message = message });
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("password-error", error);
            }
        }

        //Zalogowanie asynchroniczne 
        private async Task SignInAsync(ApplicationUser user, bool isPersistent)
        {
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ExternalCookie, DefaultAuthenticationTypes.TwoFactorCookie);
            AuthenticationManager.SignIn(new AuthenticationProperties { IsPersistent = isPersistent }, await user.GenerateUserIdentityAsync(UserManager));
        }

        public ActionResult ListaZamowien()
        {
            //Check if current user is admin
            bool IsAdmin = User.IsInRole("Admin");
            ViewBag.UserIsAdmin = IsAdmin;

            IEnumerable<Zamowienie> ZamowieniaUzytkownika;

            //Dla administratorów zwracamy wszystkie zamowienia
            if (IsAdmin)
            {
                ZamowieniaUzytkownika = db.Zamowienia.Include("PozycjeZamowienia").OrderByDescending(o => o.DataDodania).ToArray();
            }
            else
            {
                var userId = User.Identity.GetUserId();
                ZamowieniaUzytkownika = db.Zamowienia.Where(o => o.UserId == userId).Include("PozycjeZamowienia").OrderByDescending(o => o.DataDodania).ToArray();
            }

            return View(ZamowieniaUzytkownika);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public StanZamowienia ZmianaStanuZamowienia(Zamowienie zamowienie)
        {
            //Pobierz Zamowienie z Bazy
            Zamowienie zamowienieDoModyfikacji = db.Zamowienia.Find(zamowienie.ZamowienieId);
            zamowienieDoModyfikacji.StanZamowienia = zamowienie.StanZamowienia;
            db.SaveChanges();

            return zamowienie.StanZamowienia;
        }


        [Authorize(Roles = "Admin")]
        public ActionResult UkryjZwierza(int zwierzeId)
        {
            var zwierze = db.Zwierzeta.Find(zwierzeId);
            zwierze.Ukryty = true;
            db.SaveChanges();

            return RedirectToAction("DodajZwierze", new { potwierdzenie = true });
        }

        [Authorize(Roles = "Admin")]
        public ActionResult PokazZwierza(int zwierzeId)
        {
            var zwierze = db.Zwierzeta.Find(zwierzeId);
            zwierze.Ukryty = false;
            db.SaveChanges();

            return RedirectToAction("DodajZwierze", new { potwierdzenie = true });
        }


        public ActionResult DodajZwierze(int? zwierzeId, bool? potwierdzenie)
        {
            Zwierze zwierze;
            if (zwierzeId.HasValue)
            {
                //Edycja Kursu
                ViewBag.EditMode = true;
                zwierze = db.Zwierzeta.Find(zwierzeId);
            }
            else
            {
                //Dodanie nowego kursu
                ViewBag.EditMode = false;
                zwierze = new Zwierze();
            }

            var result = new EditZwierzeViewModel()
            {
                Gatunki = db.Gatunki.ToList(),
                Zwierze = zwierze,
                Potwierdzenie = potwierdzenie
            };

            return View(result);
        }


        [HttpPost]
        public ActionResult DodajZwierze(EditZwierzeViewModel model, HttpPostedFileBase file)
        {
            //Patrz pola ukryte w widoku
            if (model.Zwierze.ZwierzeId > 0)
            {
                //Modyfikacja Zwierzaka!
                db.Entry(model.Zwierze).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("DodajZwierze", new { potwierdzenie = true });
            }
            else
            {
                //Sprawdzenie czy uzytkownik wybral plik
                if (file != null || file.ContentLength > 0)
                {
                    //Czy Pozostałe pola zostały wypełnione poprawnie
                    if (ModelState.IsValid)
                    {
                        //Generowanie plik
                        var fileExt = Path.GetExtension(file.FileName);
                        var filename = Guid.NewGuid() + fileExt; // Unikalny identyfikator + rozszerzenie

                        //W jakim folderze ma byc umiesczony dany plik oraz jego nazwa! Oraz zapis
                        var path = Path.Combine(Server.MapPath(AppConfig.ObrazkiFolderWzgledny), filename);
                        file.SaveAs(path);

                        model.Zwierze.NazwaPlikuObrazka = filename;
                        model.Zwierze.DataDodania = DateTime.Now;

                        //Oczywiscie mozna wykonac standardowa procedure db.Zwierze.Add(); db.SaveChanges(), ale...
                        db.Entry(model.Zwierze).State = EntityState.Added;
                        db.SaveChanges();

                        return RedirectToAction("DodajZwierze", new { potwierdzenie = true });
                    }
                    else
                    {
                        var gatunki = db.Gatunki.ToList();
                        model.Gatunki = gatunki;
                        return View(model);
                    }

                }

                //Co gdy użytkownik nie wybral pliku
                else
                {
                    ModelState.AddModelError("", "Nie wskazano pliku");
                    //Model zostanie zwrocony, ponieważ w drpodown liście nie zostaną wyświetlone elementy! stąd musimy je jeszcze
                    //raz pobrać żeby poprostu zostały pokazane!
                    var gatunki = db.Gatunki.ToList();
                    model.Gatunki = gatunki;
                    return View(model);
                }
            }
        }
    }
}