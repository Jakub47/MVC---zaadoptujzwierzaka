﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace RolnikowePole
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                name: "ZwierzeSzczegoly",
                url: "Kurs-{id}",
                defaults: new { controller = "Zwierzeta", action = "Szczegoly" }
                );

            routes.MapRoute(
                name: "ZwierzetaList",
                url: "Gatunki/{nazwaGatunku}",
                defaults: new { controller = "Zwierzeta", action = "Lista" }
                );

            routes.MapRoute(
                name: "StronyStatyczne",
                url: "stronyStatyczne/{nazwa}.html",
                defaults: new { controller = "Home", action = "StronyStatyczne" }
                );


            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
