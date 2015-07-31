﻿/*
libspiro - conversion between spiro control points and bezier's
Copyright (C) 2007 Raph Levien
              2015 converted to C# by Wiesław Šoltés

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 3
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA
02110-1301, USA.

*/
using System;

namespace SpiroNet
{
    public struct SpiroSegment
    {
        public double X;
        public double Y;
        public SpiroPointType Type;
        public double bend_th;
        public double[] ks; // new double[4]
        public double seg_ch;
        public double seg_th;
        public double l;
    }
}
