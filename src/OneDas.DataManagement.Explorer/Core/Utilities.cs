﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace OneDas.DataManagement.Explorer.Core
{
    public static class Utilities
    {
        public static bool IsProjectAccessible(ClaimsPrincipal principal, string projectId, List<string> restrictedProjects)
        {
            var identitiy = principal.Identity;

            if (!restrictedProjects.Contains(projectId))
            {
                return true;
            }
            else if (identitiy.IsAuthenticated)
            {
                var isAdmin = principal.HasClaim(claim => claim.Type == "IsAdmin" && claim.Value == "true");
                var canAccessProjectContainer = principal.HasClaim(claim => claim.Type == "CanAccessProject"
                                                                 && claim.Value.Split(";").Any(current => current == projectId));

                return isAdmin || canAccessProjectContainer;
            }

            return false;
        }

        public static bool IsProjectVisible(ClaimsPrincipal principal, string projectId, List<string> hiddenProjects)
        {
            var identitiy = principal.Identity;

            if (!hiddenProjects.Contains(projectId))
            {
                return true;
            }
            else if (identitiy.IsAuthenticated)
            {
                var isAdmin = principal.HasClaim(claim => claim.Type == "IsAdmin" && claim.Value == "true");
                return isAdmin;
            }

            return false;
        }

        public static string GetEnumLocalization(Enum enumValue)
        {
            return EnumerationDescription.ResourceManager.GetString(enumValue.GetType().Name + "_" + enumValue.ToString());
        }

        public static List<T> GetEnumValues<T>()
        {
            return Enum.GetValues(typeof(T)).Cast<T>().ToList();
        }

        public static string FormatByteCount(long byteCount)
        {
            if (byteCount >= 1000 * 1000 * 1000)
                return $"{(double)byteCount / 1000 / 1000 / 1000:G3} GB";
            else if (byteCount >= 1000 * 1000)
                return $"{(double)byteCount / 1000 / 1000:G3} MB";
            else if (byteCount >= 1000)
                return $"{(double)byteCount / 1000:G3} kB";
            else
                return $"{(double)byteCount:F0} B";
        }
    }
}
