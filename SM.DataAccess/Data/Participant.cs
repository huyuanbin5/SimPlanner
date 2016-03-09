using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNet.Identity.EntityFramework;
using SM.Metadata;

namespace SM.DataAccess
{
    [MetadataType(typeof(ParticipantMetadata))]
    public class Participant : IdentityUser<Guid,AspNetUserLogin,AspNetUserRole,AspNetUserClaim>
    {
        #region overrides 

        public override string Email
        {
            get
            {
                return base.Email;
            }

            set
            {
                base.Email = value;
                if (string.IsNullOrEmpty(base.UserName))
                {
                    base.UserName = value;
                }
            }
        }
        #endregion //overrides


        public string AlternateEmail { get; set; }

        public string FullName { get; set; }

        public Guid DefaultDepartmentId { get; set; }

        public Guid DefaultProfessionalRoleId { get; set; }

        public virtual Department Department { get; set; }

        public virtual ProfessionalRole ProfessionalRole { get; set; }

		ICollection<CourseParticipant> _courseParticipants; 
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<CourseParticipant> CourseParticipants
		{
			get
			{
				return _courseParticipants ?? (_courseParticipants = new List<CourseParticipant>());
			}
			set
			{
				_courseParticipants = value;
			}
		}

        ICollection<ScenarioFacultyRole> _scenarioFacultyRoles;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<ScenarioFacultyRole> ScenarioFacultyRoles
        {
            get
            {
                return _scenarioFacultyRoles ?? (_scenarioFacultyRoles = new List<ScenarioFacultyRole>());
            }
            set
            {
                _scenarioFacultyRoles = value;
            }
        }

        ICollection<CourseSlotPresenter> _courseSlotPresentations;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<CourseSlotPresenter> CourseSlotPresentations
        {
            get
            {
                return _courseSlotPresentations ?? (_courseSlotPresentations = new List<CourseSlotPresenter>());
            }
            set
            {
                _courseSlotPresentations = value;
            }
        }
    }
}
