﻿using SP.Metadata;
using System;
using System.ComponentModel.DataAnnotations;

namespace SP.DataAccess
{
    [MetadataType(typeof(CourseTypeDepartmentMetadata))]
    public class CourseTypeDepartment
    {
        public Guid CourseTypeId { get; set; }
        public Guid DepartmentId { get; set; }

        public virtual CourseType CourseType { get; set; }
        public virtual Department Department { get; set; }
    }
}
