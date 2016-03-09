using SM.DataAccess;
using SM.Dto;
using System;
using System.Linq.Expressions;
namespace SM.DTOs.Maps
{
    internal static class CountryMaps
    {
        internal static Func<CountryDto, Country> mapToRepo()
        {
            return m => new Country
            {
                Code = m.Code,
                Name = m.Name,
                DialCode = m.DialCode
                //Hospitals = m.Hospitals,
                //ProfessionalRoles = m.ProfessionalRoles
            };
        }

        internal static Expression<Func<Country, CountryDto>> mapFromRepo()
        {
            return m => new CountryDto {
            Code = m.Code,
            Name = m.Name,
            DialCode = m.DialCode
            //Hospitals = m.Hospitals,
            //ProfessionalRoles = m.ProfessionalRoles
        };
    }
    }
}
