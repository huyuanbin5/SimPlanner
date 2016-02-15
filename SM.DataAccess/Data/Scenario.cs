namespace SM.DataAccess
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("Scenario")]
    public partial class Scenario
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(128)]
        public string Description { get; set; }

        public int DepartmentId { get; set; }

        public Difficulty Complexity { get; set; }

        public Emersion? EmersionCategory { get; set; }

        [StringLength(256)]
        public string TemplateFilename { get; set; }

        public int? ManequinId { get; set; }

        public int CourseTypeId { get; set; }

        public virtual Manequin Manequin { get; set; }

        public virtual CourseType CourseType { get; set; }

        public virtual Department Department { get; set; }

		ICollection<Course> _courses; 
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Course> Courses
		{
			get
			{
				return _courses ?? (_courses = new List<Course>());
			}
			set
			{
				_courses = value;
			}
		}

		ICollection<ScenarioResource> _resources; 
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<ScenarioResource> Resources
		{
			get
			{
				return _resources ?? (_resources = new List<ScenarioResource>());
			}
			set
			{
				_resources = value;
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

    }
}
