﻿using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using MoreLinq;
using RolnikowePole.App_Start;
using RolnikowePole.DAL;
using RolnikowePole.Infrastucture;
using RolnikowePole.Models;
using RolnikowePole.ViewModels;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Migrations;
using System.Data.Entity.Validation;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
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

        public ActionResult ListaWystawionychZwierzakow(Zwierze pozycjaZamowienia)
        {
            if (Request.IsAjaxRequest())
            {
                db.Entry(pozycjaZamowienia).State = EntityState.Modified;
                try
                {
                    db.SaveChanges();
                }
                catch (DbEntityValidationException e)
                {
                    foreach (var eve in e.EntityValidationErrors)
                    {
                        Debug.WriteLine("Entity of type \"{0}\" in state \"{1}\" has the following validation errors:",
                            eve.Entry.Entity.GetType().Name, eve.Entry.State);
                        foreach (var ve in eve.ValidationErrors)
                        {
                            Debug.WriteLine("- Property: \"{0}\", Error: \"{1}\"",
                                ve.PropertyName, ve.ErrorMessage);
                        }
                    }
                    throw;
                }
            }


            //Check if current user is admin
            bool IsAdmin = User.IsInRole("Admin");
            ViewBag.UserIsAdmin = IsAdmin;

            var WszystkieZwierzeta = db.Zwierzeta.ToList();
            List<string> wojewodztwa = new List<string>();
            WszystkieZwierzeta.ForEach(a =>
            {
                //When launching delete a.Wojewodztwo != null
                if (a.Wojewodztwo != null && !a.Wojewodztwo.Equals(String.Empty))
                    wojewodztwa.Add(a.Wojewodztwo);
            });

            ViewBag.Wojewodztwa = wojewodztwa.Distinct();
            ViewBag.Gatunki = db.Gatunki.ToList().Select(a => a.NazwaGatunku);

            List<Zwierze> WystawioneZwierzeta;

            //Dla administratorów zwracamy wszystkie zamowienia
            if (IsAdmin)
            {
                WystawioneZwierzeta = db.Zwierzeta.ToList();
            }
            else
            {
                var user = UserManager.FindById(User.Identity.GetUserId());
                WystawioneZwierzeta = db.Zwierzeta.Where(a => a.UserId == user.Id).ToList();
                //ZamowieniaUzytkownika = db.Zamowienia.Where(o => o.UserId == userId).Include("PozycjeZamowienia").OrderByDescending(o => o.DataDodania).ToArray();
            }

            return View(WystawioneZwierzeta);
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
            var WszystkieZwierzeta = db.Zwierzeta.ToList();
            List<string> wojewodztwa = new List<string>();
            WszystkieZwierzeta.ForEach(a =>
            {
                //When launching delete a.Wojewodztwo != null
                if (a.Wojewodztwo != null && !a.Wojewodztwo.Equals(String.Empty))
                    wojewodztwa.Add(a.Wojewodztwo);
            });

            ViewBag.Wojewodztwa = wojewodztwa.Distinct();
            ViewBag.PierwszeWojewodztwo = wojewodztwa.Distinct().First();


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

            zwierze.DataNarodzin = DateTime.Now;
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
                
                //Co gdy użytkownik nie wybral pliku
                if (file == null)
                {
                    var WszystkieZwierzeta = db.Zwierzeta.ToList();
                    List<string> wojewodztwa = new List<string>();
                    WszystkieZwierzeta.ForEach(a =>
                    {
                        //When launching delete a.Wojewodztwo != null
                        if (a.Wojewodztwo != null && !a.Wojewodztwo.Equals(String.Empty))
                            wojewodztwa.Add(a.Wojewodztwo);
                    });

                    ViewBag.Wojewodztwa = wojewodztwa.Distinct();
                    ViewBag.PierwszeWojewodztwo = wojewodztwa.Distinct().First();
                    //Model zostanie zwrocony, ponieważ w drpodown liście nie zostaną wyświetlone elementy! stąd musimy je jeszcze
                    //raz pobrać żeby poprostu zostały pokazane!
                    var gatunki = db.Gatunki.ToList();
                    model.Gatunki = gatunki;
                    return View(model);
                }
                //Sprawdzenie czy uzytkownik wybral plik
                else
                {
                    //Czy Pozostałe pola zostały wypełnione poprawnie
                    if (ModelState.IsValid)
                    {
                        var sourceImage = Image.FromStream(file.InputStream);

                        sourceImage = ResizeImage(sourceImage, 700, 400);

                        //Generowanie plik
                        var fileExt = Path.GetExtension(file.FileName);
                        var filename = Guid.NewGuid() + fileExt; // Unikalny identyfikator + rozszerzenie

                        //W jakim folderze ma byc umiesczony dany plik oraz jego nazwa! Oraz zapis
                        var path = Path.Combine(Server.MapPath(AppConfig.ObrazkiFolderWzgledny), filename);
                        //file.SaveAs(path);
                        sourceImage.Save(path);

                        model.Zwierze.NazwaPlikuObrazka = filename;
                        model.Zwierze.DataDodania = DateTime.Now;
                        model.Zwierze.UserId = User.Identity.GetUserId();
                        //Oczywiscie mozna wykonac standardowa procedure db.Zwierze.Add(); db.SaveChanges(), ale...
                        db.Entry(model.Zwierze).State = EntityState.Added;
                        var user = UserManager.FindById(User.Identity.GetUserId());

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

            }
        }

        /// <summary>
        /// Resize the image to the specified width and height.
        /// </summary>
        /// <param name="image">The image to resize.</param>
        /// <param name="width">The width to resize to.</param>
        /// <param name="height">The height to resize to.</param>
        /// <returns>The resized image.</returns>
        [NonAction]
        public static Bitmap ResizeImage(Image image, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }

        public ActionResult WyswietlWiadomosciUzytkownika()
        {
            var user = UserManager.FindById(User.Identity.GetUserId());

            //Jak ja wysyłam to mnie nie obchodzi wysyłający tylko odbiorca
            //Jak ja odbieram to mnie nie obchodzi odbierający tylko wysyłający

            //List of all users id !
            //var GetUsersIDSended = user.SenderMessages.Where(a => a.ReceiverId != user.Id).Select(b => b.ReceiverId).ToList();
            //var GetUsersIDRetrived = user.ReceiverMessages.Where(a => a.SenderId != user.Id).Select(b => b.SenderId).ToList();


            //Potrzebuje nazwyUzytkownika + Daty + Tresci kazdej wiadomosci
            var wiadomosci = new List<WiadomosciViewModel>();
            var w = new WiadomosciOdzieloneViewModel();

            var wiadomosciWyslane = user.SenderMessages.Where(a => a.SenderId == user.Id).OrderByDescending(a => a.DateAndTimeOfSend).DistinctBy(a => a.ZwierzeId).ToList();

            //GIT
            var wiadomosciOtrzymane = user.ReceiverMessages.Where(a => a.ReceiverId == user.Id).OrderByDescending(a => a.DateAndTimeOfSend).DistinctBy(a => a.ZwierzeId).ToList();

            wiadomosciWyslane.ForEach(a =>
            {
                //wiadomosci.Add(new WiadomosciViewModel()
                //{
                //    NazwaUzytkownika = a.Receiver.DaneUzytkownika.Imie + " " +  a.Receiver.DaneUzytkownika.Nazwisko,
                //    DataWyslania = a.DateAndTimeOfSend,
                //    TrescWiadomosci = a.Body
                //});

                var z = new WiadomosciViewModel()
                {
                    NazwaUzytkownika = a.Receiver.DaneUzytkownika.Imie + " " + a.Receiver.DaneUzytkownika.Nazwisko,
                    DataWyslania = a.DateAndTimeOfSend,
                    TrescWiadomosci = a.Body,
                    Id = a.ReceiverId,
                    Zwierze = db.Zwierzeta.Where(b => b.ZwierzeId == a.ZwierzeId).FirstOrDefault()
                };

                w.WiadomosciWyslane.Add(z);
            });

            wiadomosciOtrzymane.ForEach(a =>
            {
                //wiadomosci.Add(new WiadomosciViewModel()
                //{
                //    NazwaUzytkownika = a.Receiver.DaneUzytkownika.Imie + " " + a.Receiver.DaneUzytkownika.Nazwisko,
                //    DataWyslania = a.DateAndTimeOfSend,
                //    TrescWiadomosci = a.Body
                //});

                var z = new WiadomosciViewModel()
                {
                    NazwaUzytkownika = a.Receiver.DaneUzytkownika.Imie + " " + a.Receiver.DaneUzytkownika.Nazwisko,
                    DataWyslania = a.DateAndTimeOfSend,
                    TrescWiadomosci = a.Body,
                    Id = a.SenderId,
                    Zwierze = db.Zwierzeta.Where(b => b.ZwierzeId == a.ZwierzeId).FirstOrDefault()
                };

                w.WiadomosciOtrzymane.Add(z);
            });


            return View(w);

            //var WszyscyUserzy = UserManager.Users;

            //foreach (var userr in UserManager.Users)
            //{
            //    wiadomosciOtrzymane.ForEach(a =>
            //    {

            //    });
            //}

            //Wziąć ostatnią wiadomość i id użytkownika innego niz zalogowany 
            //Co wziac


            //var wiadomoscVM = new WiadomosciViewModel
            //{
            //    WiadomosciWyslane = wiadomosciWyslane,
            //    WiadomosciOtrzymane = wiadomosciOtrzymane
            //};

            //return View(wiadomoscVM);
        }

        public ActionResult WiadomosciKonwersacja(int idZwierza, string idUser, bool Otrzymane = false)
        {
            //  Jezeli jest to id uzytkownika wiemy ze powinnismy zwrococ wiadomosci tego uzytkownika a jesli chodzi o 
            //innych uzytkownikow to tam przekazujemy sernderID = isUser
            var userLogged = UserManager.FindById(User.Identity.GetUserId());
            var userDiffrent = UserManager.FindById(idUser);
            var wszystkieWiadomosci = new List<Wiadomosc>();

            if (Otrzymane)
            {
                //var mojeWiadomosci = userLogged.SenderMessages.Where(a => a.ZwierzeId == idZwierza && a.ReceiverId == idUser).ToList();
                //var inneWiadomosci = userDiffrent.ReceiverMessages.Where(a => a.ZwierzeId == idZwierza && a.SenderId == userLogged.Id).ToList();
                //mojeWiadomosci.ForEach(a => wszystkieWiadomosci.Add(a)); inneWiadomosci.ForEach(a => wszystkieWiadomosci.Add(a));

                wszystkieWiadomosci = db.Wiadomosci.Where(a => a.ZwierzeId == idZwierza && (a.ReceiverId == userLogged.Id
                                                     && a.SenderId == userDiffrent.Id)).ToList();
            }
            else
            {
                //var mojeWiadomosci = userLogged.SenderMessages.Where(a => a.ZwierzeId == idZwierza && a.ReceiverId == idUser).ToList();
                //var inneWiadomosci = userDiffrent.SenderMessages.Where(a => a.ZwierzeId == idZwierza && a.SenderId == userLogged.Id).ToList();
                //mojeWiadomosci.ForEach(a => wszystkieWiadomosci.Add(a)); inneWiadomosci.ForEach(a => wszystkieWiadomosci.Add(a));
                wszystkieWiadomosci = db.Wiadomosci.Where(a => a.ZwierzeId == idZwierza && (a.ReceiverId == userDiffrent.Id
                                                     && a.SenderId == userLogged.Id)).ToList();
            }

            //var listaMoichWiadomosci = new List<Wiadomosc>();
            //var listaInnychWiadomosci = new List<Wiadomosc>();
            //var userId = User.Identity.GetUserId();

            //var listaWiadomosci = db.Wiadomosci.Where(a => a.ZwierzeId == idZwierza && a.ReceiverId == idUser && a.SenderId == userId)
            //                            .ToList();

            //idUser == gosc do ktorego sie wysyla ... userId == mojeId

            //if (czyIdUzytkownika)
            //{
            //   listaMoichWiadomosci = db.Wiadomosci.Where(a => a.ZwierzeId == idZwierza && a.SenderId == idUser).ToList();
            //   listaInnychWiadomosci = db.Wiadomosci.Where(a => a.ZwierzeId == idZwierza && a.Re
            //}
            //else
            //{
            //    var listaMoichWiadomos
            //}

            //Chcę wziąć wszystkie wiadmości odnośnie konkretnego zwierza i od określonej osoby

            //a.ReceiverId.Equals(idReceiverId, StringComparison.CurrentCultureIgnoreCase)
            //|| (a.SenderId.Equals(idSenderID, StringComparison.CurrentCultureIgnoreCase)))).ToList();

            //Pobierz wiadomosci które ja wysłałem do tej osoby
            //var listaWiadomosciOsobyWysylającej = db.Wiadomosci.Where(a => a.ZwierzeId == idZwierza && )

            //Pobierz wiadomosci które ona wysłałą do mnie!
            var wWiadomosc = new Wiadomosc
            {
                ZwierzeId = idZwierza,
                SenderId = userLogged.Id,
                ReceiverId = userDiffrent.Id
            };

            var vm = new WiadomoscViewModel
            {
                ListaWiadomosci = wszystkieWiadomosci,
                wiadomosc = wWiadomosc
            };

            return View(vm);
        }

        [HttpPost]
        public ActionResult WyslijWiadomosc(Wiadomosc wiadomosc)
        {
            //db.Users.Find(wiadomosc.ReceiverId).ReceiverMessages.Add(wiadomosc);
            //db.Users.Find(wiadomosc.SenderId).SenderMessages.Add(wiadomosc);
            wiadomosc.DateAndTimeOfSend = DateTime.Now;

            db.Wiadomosci.AddOrUpdate(wiadomosc);

            try
            {
                db.SaveChanges();
            }
            catch (DbEntityValidationException ex)
            {
                foreach (DbEntityValidationResult item in ex.EntityValidationErrors)
                {
                    // Get entry

                    DbEntityEntry entry = item.Entry;
                    string entityTypeName = entry.Entity.GetType().Name;

                    // Display or log error messages

                    foreach (DbValidationError subItem in item.ValidationErrors)
                    {
                        string message = string.Format("Error '{0}' occurred in {1} at {2}",
                                 subItem.ErrorMessage, entityTypeName, subItem.PropertyName);
                        //Console.WriteLine(message);
                        System.Diagnostics.Debug.WriteLine(message);
                    }
                }
            }
            var idZwierza = wiadomosc.ZwierzeId;
            string idReceiverId = wiadomosc.ReceiverId;
            string idSenderID = wiadomosc.SenderId;

            //Chcę wziąć wszystkie wiadmości odnośnie konkretnego zwierza i od określonej osoby
            var listaWiadomosci = db.Wiadomosci.Where(a => a.ZwierzeId == idZwierza && (a.ReceiverId.Equals(idReceiverId, StringComparison.CurrentCultureIgnoreCase)
                                                     || (a.SenderId.Equals(idSenderID, StringComparison.CurrentCultureIgnoreCase)))).ToList();

            //var mojeWiadomosci = userLogged.SenderMessages.Where(a => a.ZwierzeId == idZwierza && a.ReceiverId == idUser).ToList();
            //var inneWiadomosci = userDiffrent.SenderMessages.Where(a => a.ZwierzeId == idZwierza && a.SenderId == userLogged.Id).ToList();
            var wszystkieWiadomosci = db.Wiadomosci.Where(a => a.ZwierzeId == idZwierza && (a.ReceiverId == idReceiverId &&
                                                     a.SenderId == idSenderID)).ToList();
            //var inneWiadomosci = db.Wiadomosci.Where(a => a.ZwierzeId == idZwierza && a.SenderId == idSenderID).ToList();
            return View("_Wiadomosci", wszystkieWiadomosci);
        }

    }
}