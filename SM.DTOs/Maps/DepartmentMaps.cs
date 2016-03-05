using SM.DataAccess;
using SM.Dto;
using System;
using System.Linq.Expressions;
namespace SM.DTOs.Maps
{
    public static class DepartmentMaps
    {
        public static Func<DepartmentDto, Department> mapToRepo = m => new Department
        {
            Id = m.Id,
            Name = m.Name,
            InstitutionId = m.InstitutionId,
            InvitationLetterFilename = m.InvitationLetterFilename,
            CertificateFilename = m.CertificateFilename
        };


        public static Expression<Func<Department, DepartmentDto>> mapFromRepo = m => new DepartmentDto
        {
            Id = m.Id,
            Name = m.Name,
            InstitutionId = m.InstitutionId,
            InvitationLetterFilename = m.InvitationLetterFilename,
            CertificateFilename = m.CertificateFilename,

            //CourseTypes = null,
            //Institution = m.Institution,
            //Manequins = m.Manequins,
            //Courses = m.Courses,
            //Scenarios = m.Scenarios,
            //Departments = null
        };
    }
}
