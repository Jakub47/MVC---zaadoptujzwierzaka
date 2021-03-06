﻿using RolnikowePole.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace RolnikowePole.ViewModels
{
    public class ZwierzetaGatunkiViewModel
    {
        public IEnumerable<Gatunek> Gatunki { get; set; }
        public IEnumerable<Zwierze> ZwierzetaGatunku { get; set; }
    }
}