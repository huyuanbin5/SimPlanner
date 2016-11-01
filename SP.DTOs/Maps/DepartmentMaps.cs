using SP.DataAccess;

namespace SP.Dto.Maps
{
    internal class DepartmentMaps : DomainDtoMap<Department, DepartmentDto>
    {
        public DepartmentMaps() : base(m => new Department
            {
                Id = m.Id,
                Name = m.Name,
                InstitutionId = m.InstitutionId,
                InvitationLetterFilename = m.InvitationLetterFilename,
                CertificateFilename = m.CertificateFilename,
                Abbreviation = m.Abbreviation,
                PrimaryColour = m.PrimaryColour,
                SecondaryColour = m.SecondaryColour,
                AdminApproved = m.AdminApproved
            },
            m => new DepartmentDto
            {
                Id = m.Id,
                Name = m.Name,
                InstitutionId = m.InstitutionId,
                InvitationLetterFilename = m.InvitationLetterFilename,
                CertificateFilename = m.CertificateFilename,
                Abbreviation = m.Abbreviation,
                PrimaryColour = m.PrimaryColour,
                SecondaryColour = m.SecondaryColour,
                AdminApproved = m.AdminApproved
                //CourseTypes = null,
                //Institution = m.Institution,
                //Manikins = m.Manikins,
                //Courses = m.Courses,
                //Scenarios = m.Scenarios,
                //Departments = null
            })
        { }
    }
}
