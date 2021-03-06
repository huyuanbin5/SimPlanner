﻿using Breeze.ContextProvider;
using b = Breeze.ContextProvider;
using LinqKit;
using SP.DataAccess;
using SP.DataAccess.Data.Interfaces;
using SP.Dto.Maps;
using SP.Dto.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Data.Entity.Validation;
using NLog;
using Newtonsoft.Json;

namespace SP.Dto.ProcessBreezeRequests
{
    internal sealed class ValidateMedSim
    {
        public ValidateMedSim(CurrentPrincipal currentUser)
        {
            CurrentUser = currentUser;
        }
        public CurrentPrincipal CurrentUser { get; private set; }
        public MedSimDbContext Context { get {
                return CurrentUser.Context;
            } }
        private const string _afterUpdateDelegate = "_afterUpdateDelegate";
        private const string _preSaveState = "_preSaveState";

        /*
        private static IEnumerable<PropertyInfo> _identityUserDbProperties;
        private static IEnumerable<PropertyInfo> IdentityUserDbProperties
        {
            get
            {
                return _identityUserDbProperties ??
                    (_identityUserDbProperties = typeof(IdentityUser<Guid, AspNetUserLogin, AspNetUserRole, AspNetUserClaim>)
                        .GetProperties(BindingFlags.DeclaredOnly |
                                           BindingFlags.Public |
                                           BindingFlags.Instance));
            }
        }
        */

        public Func<Participant, string, IEnumerable<string>> CreateUser { get; set; }
        public Action<IEnumerable<BookingChangeDetails>> AfterBookingChange { get; set; }
        public Action<UserRequestingApproval> AfterNewUnapprovedUser { get; set; }
        public Action<Participant> AfterUserApproved { get; set; }
        public Action<IEnumerable<CourseParticipant>> AfterNewCourseParticipant { get; set; }
        /// <summary>
        /// courseId, originalStart - null if newly created course
        /// </summary>
        public Action<Guid, DateTime?> AfterCourseDateChange { get; set; }

        private static ILogger _logger = LogManager.GetCurrentClassLogger();

        public void ValidateDto(Dictionary<Type, List<EntityInfo>> saveMap)
        {
            List<EntityError> errors = new List<EntityError>();
            var settings = new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };

            _logger.Debug(() => $"validating:\r\n{JsonConvert.SerializeObject(saveMap.ToDictionary(sm=>sm.Key.Name, sm=>sm.Value.Select(v=>new { v.EntityState, v.Entity, v.OriginalValuesMap })), Formatting.Indented, settings)}");
            foreach (var v in saveMap.Values.SelectMany(sm=>sm))
            {
                if (v.UnmappedValuesMap == null)
                {
                    v.UnmappedValuesMap = new Dictionary<string, object>();
                }
                v.UnmappedValuesMap.Add(_preSaveState, v.EntityState);
            }

#pragma warning disable IDE0018 // Inline variable declaration
            List<EntityInfo> currentInfos;
#pragma warning restore IDE0018 // Inline variable declaration

            if (saveMap.TryGetValue(typeof(ActivityDto), out currentInfos))
            {
                errors.AddRange(GetActivityErrors(currentInfos));
            }

            if (saveMap.TryGetValue(typeof(CandidatePrereadingDto), out currentInfos))
            {
                errors.AddRange(GetCandidatePrereadingErrors(currentInfos));
            }

            if (saveMap.TryGetValue(typeof(CourseFormatDto), out currentInfos))
            {
                errors.AddRange(GetCourseFormatErrors(currentInfos));
            }

            if (saveMap.TryGetValue(typeof(InstitutionDto), out currentInfos))
            {
                errors.AddRange(GetInstitutionErrors(currentInfos));
            }

            if (saveMap.TryGetValue(typeof(Participant), out currentInfos))
            {
                errors.AddRange(GetParticipantErrors(currentInfos));
            }

            if (saveMap.TryGetValue(typeof(RoomDto), out currentInfos))
            {
                errors.AddRange(GetRoomErrors(currentInfos));
            }

            if (saveMap.TryGetValue(typeof(ScenarioResourceDto), out currentInfos))
            {
                errors.AddRange(GetScenarioResourceErrors(currentInfos));
            }

            errors.AddRange(ValidatePermission(saveMap));

            if (errors.Any())
            {
                throw new EntityErrorsException(errors);
            }


        }
        bool HasCoursePermission(Guid courseId)
        {
            return HasDepartmentPermission(from c in Context.Courses
                                           where c.Id == courseId
                                           select c.DepartmentId);
        }

        bool HasCourseTypePermission(Guid courseTypeId)
        {
            return HasDepartmentPermission(from c in Context.CourseTypeDepartments
                                           where c.CourseTypeId == courseTypeId
                                           select c.DepartmentId);
        }
        bool HasDepartmentPermission(Guid departmentId)
        {
            return HasDepartmentPermission((new[] { departmentId }).AsQueryable());
        }
        bool HasDepartmentPermission(IQueryable<Guid> departmentIds)
        {
            switch (CurrentUser.AdminLevel)
            {
                case AdminLevels.AllData:
                    return true;
                case AdminLevels.None:
                    return false;
                case AdminLevels.DepartmentAdmin:
                case AdminLevels.InstitutionAdmin:
                    return departmentIds.Any(d=>CurrentUser.UserDepartmentAdminIds.Contains(d));
            }
            throw new UnauthorizedAccessException("Unknown Admin Level");
        }

        bool HasInstitutionPermission(Guid institutionId)
        {
            switch (CurrentUser.AdminLevel)
            {
                case AdminLevels.AllData:
                    return true;
                case AdminLevels.None:
                case AdminLevels.DepartmentAdmin:
                    return false;
                case AdminLevels.InstitutionAdmin:
                    return CurrentUser.UserInstitutionId == institutionId;
            }
            throw new UnauthorizedAccessException("Unknown Admin Level");
        }

        private static IEnumerable<EntityError> PermissionErrors<T>(Dictionary<Type, List<EntityInfo>> saveMap, Func<T, bool> isPermitted, string propertyName = null)
        {
            return PermissionErrors<T>(saveMap, (t, e) => isPermitted(t));
        }
        private static IEnumerable<EntityError> PermissionErrors<T>(Dictionary<Type, List<EntityInfo>> saveMap, Func<T, b.EntityState,bool> isPermitted, string propertyName = null)
        {
            if (saveMap.TryGetValue(typeof(T), out List<EntityInfo> eis))
            {
                return (from ei in eis
                        let e = (T)ei.Entity
                        where !isPermitted(e, ei.EntityState)
                        select MappedEFEntityError.Create(e, "insufficientPermission", "you do not have permission to update this record", propertyName));
            }
            return Enumerable.Empty<EntityError>();
        }

        public IEnumerable<EntityError> ValidatePermission(Dictionary<Type, List<EntityInfo>> saveMap)
        {
            return PermissionErrors<ActivityDto>(saveMap,
                a => HasDepartmentPermission(from ca in Context.CourseActivities
                                             where ca.Id == a.CourseActivityId
                                             from ctd in ca.CourseType.CourseTypeDepartments
                                             select ctd.DepartmentId))
                .Concat(PermissionErrors<CandidatePrereadingDto>(saveMap,
                c => HasCourseTypePermission(c.CourseTypeId)))
                .Concat(PermissionErrors<CultureDto>(saveMap,
                e => CurrentUser.AdminLevel >= AdminLevels.InstitutionAdmin))
                .Concat(PermissionErrors<CourseDto>(saveMap,
                e => HasDepartmentPermission(e.DepartmentId)))
                .Concat(PermissionErrors<CourseActivityDto>(saveMap,
                e => HasCourseTypePermission(e.CourseTypeId)))
                .Concat(PermissionErrors<CourseFormatDto>(saveMap,
                e => HasCourseTypePermission(e.CourseTypeId)))
                .Concat(PermissionErrors<CourseFacultyInviteDto>(saveMap,
                e => HasCoursePermission(e.CourseId)))
                .Concat(PermissionErrors<CourseParticipantDto>(saveMap,
                e => HasCoursePermission(e.CourseId) || (e.ParticipantId == CurrentUser.Principal.Id && Context.CourseFacultyInvites.Any(cfi=>cfi.CourseId == e.CourseId && cfi.ParticipantId == e.ParticipantId))))
                .Concat(PermissionErrors<CourseScenarioFacultyRoleDto>(saveMap,
                e => HasCoursePermission(e.CourseId)))
                .Concat(PermissionErrors<CourseSlotDto>(saveMap,
                e => HasDepartmentPermission(from c in Context.CourseFormats
                                             where c.Id == e.CourseFormatId
                                             from ctd in c.CourseType.CourseTypeDepartments
                                             select ctd.DepartmentId)))
                .Concat(PermissionErrors<CourseSlotActivityDto>(saveMap,
                e => HasCoursePermission(e.CourseId)))
                .Concat(PermissionErrors<CourseSlotManikinDto>(saveMap,
                e => HasCoursePermission(e.CourseId)))
                .Concat(PermissionErrors<CourseSlotPresenterDto>(saveMap,
                e => HasCoursePermission(e.CourseId)))
                .Concat(PermissionErrors<CourseTypeDto>(saveMap,
                e => HasCourseTypePermission(e.Id)))
                //issue of who can share a course type with others
                //perhaps a primary creator property should be added
                .Concat(PermissionErrors<CourseTypeDepartmentDto>(saveMap,
                e => HasCourseTypePermission(e.CourseTypeId)))
                .Concat(PermissionErrors<CourseTypeScenarioRoleDto>(saveMap,
                e => HasCourseTypePermission(e.CourseTypeId)))
                //possibilities:
                //unregistered user applying to join/register themself + department: set approved false
                //department being modified - usual inst & dpt admin priveleges
                //department being created - code should still work with iqueryable.contains
                .Concat(PermissionErrors<DepartmentDto>(saveMap,
                (e, state) => {
                    if (state == b.EntityState.Added)
                    {
                        e.AdminApproved = e.AdminApproved && HasInstitutionPermission(e.InstitutionId);
                        return true;
                    }
                    else
                    {
                        var existingInstId = Context.Departments.Find(e.Id).InstitutionId;
                        return HasDepartmentPermission(e.Id) && existingInstId == e.InstitutionId;
                    }
                }))
                .Concat(PermissionErrors<FacultyScenarioRoleDto>(saveMap,
                e => HasDepartmentPermission(from ctsr in Context.CourseTypeScenarioRoles
                                             where ctsr.FacultyScenarioRoleId == e.Id
                                             from ctd in ctsr.CourseType.CourseTypeDepartments
                                             select ctd.DepartmentId)))
                .Concat(PermissionErrors<HotDrinkDto>(saveMap,
                e => CurrentUser.AdminLevel >= AdminLevels.InstitutionAdmin))
                .Concat(PermissionErrors<InstitutionDto>(saveMap,
                (e, state) => {
                    if (state == b.EntityState.Added)
                    {
                        e.AdminApproved = e.AdminApproved && CurrentUser.AdminLevel == AdminLevels.AllData;
                        return true;
                    }
                    else
                    {
                        return HasInstitutionPermission(e.Id);
                    }
                }))
                .Concat(PermissionErrors<ManikinDto>(saveMap,
                e => HasDepartmentPermission(e.DepartmentId)))
                .Concat(PermissionErrors<ManikinManufacturerDto>(saveMap,
                e => CurrentUser.AdminLevel >= AdminLevels.InstitutionAdmin))
                //TODO: neaten up
                //for now, any update or create operation
                .Concat(PermissionErrors<ParticipantDto>(saveMap,
                (e, state) => {
                    if (state == b.EntityState.Deleted)
                    {
                        return CurrentUser.AdminLevel == AdminLevels.AllData || e.Id == CurrentUser.Principal.Id;
                    }
                    if (state == b.EntityState.Added)
                    {
                        return CurrentUser.AdminLevel != AdminLevels.None || !e.AdminApproved;
                    } 
                    if (state == b.EntityState.Modified)
                    {
                        if (CurrentUser.AdminLevel != AdminLevels.None) {
                            return true;
                        };
                        return (e.Id == CurrentUser.Principal.Id && CurrentUser.Principal.AdminApproved);
                    }
                    return true;
                }))
                .Concat(PermissionErrors<ProfessionalRoleDto>(saveMap,
                e => CurrentUser.AdminLevel >= AdminLevels.InstitutionAdmin))
                .Concat(PermissionErrors<ProfessionalRoleInstitutionDto>(saveMap,
                e => HasInstitutionPermission(e.InstitutionId)))
                .Concat(PermissionErrors<RoomDto>(saveMap,
                e => HasDepartmentPermission(e.DepartmentId)))
                .Concat(PermissionErrors<ScenarioDto>(saveMap,
                e => HasDepartmentPermission(e.DepartmentId)))
                .Concat(PermissionErrors<ScenarioResourceDto>(saveMap,
                e => HasDepartmentPermission(from s in Context.Scenarios
                                             where s.Id == e.ScenarioId
                                             select s.DepartmentId)))
                .Concat(PermissionErrors<UserRoleDto>(saveMap,
                e => {
                    switch ( RoleConstants.RoleNames[e.RoleId] ) {
                        case RoleConstants.SiteAdmin:
                            return (CurrentUser.OtherRoles & AditionalRoles.SiteAdmin)!=0;
                        case RoleConstants.AccessAllData:
                            return CurrentUser.AdminLevel == AdminLevels.AllData;
                        case RoleConstants.AccessInstitution:
                            if (CurrentUser.AdminLevel == AdminLevels.AllData) {
                                return true;
                            }
                            var departmentId = DepartmentForUser(e.UserId, saveMap);
                            return CurrentUser.AdminLevel >= AdminLevels.InstitutionAdmin
                                    && departmentId.HasValue && CurrentUser.UserDepartmentAdminIds.Contains(departmentId.Value);
                        case RoleConstants.AccessDepartment:
                            if (CurrentUser.AdminLevel == AdminLevels.AllData)
                            {
                                return true;
                            }
                            var dptId = DepartmentForUser(e.UserId, saveMap);
                            return dptId.HasValue && (CurrentUser.AdminLevel >= AdminLevels.InstitutionAdmin
                                        || CurrentUser.AdminLevel == AdminLevels.DepartmentAdmin)
                                    && CurrentUser.UserDepartmentAdminIds.Contains(dptId.Value);
                        case RoleConstants.DptManikinBookings:
                        case RoleConstants.DptRoomBookings:
                            return CurrentUser.AdminLevel != AdminLevels.None || CurrentUser.OtherRoles > AditionalRoles.None;
                        default:
                            throw new NotImplementedException("unknown role Id");

                    } }));
        }
        private Guid? DepartmentForUser(Guid userId, Dictionary<Type, List<EntityInfo>> saveMap)
        {
            Guid? returnVar;
            if (saveMap.TryGetValue(typeof(ParticipantDto), out List<EntityInfo> ei))
            {
                returnVar = (from e in ei
                             let ent = (ParticipantDto)e.Entity
                             where ent.Id == userId
                             select (Guid?)ent.DefaultDepartmentId).FirstOrDefault();
            }
            else
            {
                returnVar = null;
            }

            if (!returnVar.HasValue)
            {
                returnVar = Context.Users.Find(userId)?.DefaultDepartmentId;
            }
            return returnVar;
        }
        public Dictionary<Type, List<EntityInfo>> MapDtoToServerType(Dictionary<Type, List<EntityInfo>> saveMap)
        {
            var returnVar = new Dictionary<Type, List<EntityInfo>>();

            foreach (var kv in saveMap)
            {
                List<EntityInfo> vals;
                if (kv.Key == typeof(ParticipantDto))
                {
                    vals = kv.Value.Select(v=>UserManager(v)).ToList();
                }
                else
                {
                    var mapper = MapperConfig.GetFromDtoMapper(kv.Key);
                    vals = kv.Value.Select(d =>
                    {
                        var rv = d.ContextProvider.CreateEntityInfo(mapper(d.Entity), d.EntityState);
                        rv.OriginalValuesMap = d.OriginalValuesMap;
                        rv.UnmappedValuesMap = d.UnmappedValuesMap;
                        return rv;
                    }).ToList();
                }
                returnVar.Add(MapperConfig.GetServerModelType(kv.Key), vals);
            }
            return returnVar;
        }

        public void AfterSave(Dictionary<Type, List<EntityInfo>> saveMap, List<KeyMapping> maps)
        {
            if (saveMap.TryGetValue(typeof(CourseSlot), out List<EntityInfo> ei))
            {
                UpdateICourseDays(ei.Select(e => (CourseSlot)e.Entity));
            }

            if (saveMap.TryGetValue(typeof(Participant), out ei))
            {
                var participants = TypedEntityInfo<Participant>.GetTyped(ei);
                if (AfterNewUnapprovedUser != null)
                {
                    Participant participant = participants.FirstOrDefault(e => e.Info.UnmappedValuesMap[_preSaveState].Equals(b.EntityState.Added) && !e.Entity.AdminApproved)?.Entity;
                    if (participant != null)
                    {
                        //this is where having the breeze context and the validation cotext looks a little messy
                        //could just get the department as an untracked entity and manually add it on to the participant, but that seems even messier
                        participant = Context.Users.Include(u => u.Department.Institution).AsNoTracking().First(u => u.Id == participant.Id);
                        var siteAdminId = (from r in RoleConstants.RoleNames where r.Value == RoleConstants.SiteAdmin select r.Key).First();
                        var admins = Context.Users.Where(u => u.Roles.Any(r => r.RoleId == siteAdminId)).ToList();
                        AfterNewUnapprovedUser.Invoke(new UserRequestingApproval {
                            User = participant,
                            Administrators = admins
                        });
                    }
                }
                if (AfterUserApproved != null)
                {
                    Participant participant;
                    object obj = null;
                    participant = participants.FirstOrDefault(e => (e.Info.UnmappedValuesMap[_preSaveState].Equals(b.EntityState.Modified)
                            && e.Entity.AdminApproved && e.Info.OriginalValuesMap.TryGetValue(nameof(participant.AdminApproved), out obj) && obj.Equals(false))
                        || (e.Info.EntityState == b.EntityState.Added && e.Info.UnmappedValuesMap?.TryGetValue("emailOnCreate", out obj)==true && obj.Equals(true)))?.Entity;
                    if (participant != null)
                    {
                        AfterUserApproved.Invoke(participant);
                    }
                }
            }

            AfterBookingChange?.Invoke(GetBookingChanges(saveMap));

            if (AfterCourseDateChange != null 
                && saveMap.TryGetValue(typeof(Course), out ei))
            {
                var te = TypedEntityInfo<Course>.GetTyped(ei);
                foreach (var e in te)
                {
                    if (e.Info.UnmappedValuesMap[_preSaveState].Equals(b.EntityState.Modified))
                    {
                        if (e.Info.OriginalValuesMap.TryGetValue(nameof(e.Entity.StartFacultyUtc), out object originalStart)
    && !originalStart.Equals(e.Entity.StartFacultyUtc))
                        {
                            AfterCourseDateChange(e.Entity.Id, (DateTime)originalStart);
                        }
                    }
                    else if (e.Info.UnmappedValuesMap[_preSaveState].Equals(b.EntityState.Added))
                    {
                        AfterCourseDateChange(e.Entity.Id, null);
                    }
                }
            }

            if (AfterNewCourseParticipant != null && saveMap.TryGetValue(typeof(CourseParticipant), out ei))
            {
                var cps = (from cp in TypedEntityInfo<CourseParticipant>.GetTyped(ei)
                           where cp.Info.UnmappedValuesMap[_preSaveState].Equals(b.EntityState.Added)
                           select cp.Entity).ToList();
                if (cps.Count > 0)
                {
                    AfterNewCourseParticipant.Invoke(cps);
                }
            }

            foreach (var mei in saveMap.Values.SelectMany(e => e))
            {
                object pred = null;
                if (mei.UnmappedValuesMap?.TryGetValue(_afterUpdateDelegate, out pred) == true){
                    ((Action)pred).Invoke();
                }
            }

            
            
            var iAssocFiles = (from s in saveMap
                               where typeof(IAssociateFile).IsAssignableFrom(s.Key) 
                               select TypedEntityInfo<IAssociateFile>.GetTyped(s.Value))
                               .SelectMany(s=>s).ToLookup(k => (b.EntityState)k.Info.UnmappedValuesMap[_preSaveState]);

            foreach (var i in iAssocFiles[b.EntityState.Deleted]) {
                Equals(b.EntityState.Detached, i.Info.EntityState);
                i.Entity.DeleteFile();
            }

            foreach (var i in iAssocFiles[b.EntityState.Modified])
            {
                //? need to check lastmodified or size?
                string originalFilename = (string)i.Info.OriginalValuesMap.TryGetFirstValue(nameof(i.Entity.FileName), "LogoImageFileName");
                if (originalFilename != null && i.Entity.FileName != originalFilename)
                {
                    string fileName = i.Entity.FileName;
                    //bit of a hack
                    i.Entity.FileName = originalFilename;
                    i.Entity.DeleteFile();
                    i.Entity.FileName = fileName;
                }
            }

            foreach (var i in iAssocFiles[b.EntityState.Added].Concat(iAssocFiles[b.EntityState.Modified]))
            {
                if (i.Entity.File != null)
                {
                    var o = i.Entity as IAssociateFileOptional;
                    if (o == null)
                    {
                        ((IAssociateFileRequired)i.Entity).StoreFile();
                    }
                    else
                    {
                        o.StoreFile();
                    }
                    i.Entity.File = null;
                }
            }
        }

        /*
private void AddApprovedRole(List<EntityInfo> currentInfos)
{
   var participants = from ci in currentInfos
                            where ci.EntityState == b.EntityState.Added
                            select ((Participant)ci.Entity).Id;
   foreach (var p in participants)
   {
       Context.UserRoles.Add(new AspNetUserRole { UserId = p, RoleId = Guid.ParseExact(RoleConstants.AdminApprovedId, RoleConstants.IdFormat) });
   }
}
*/
/// <summary>
/// 
/// </summary>
/// <param name="currentInfo"></param>
/// <returns>null if managed outside breeze context</returns>
        EntityInfo UserManager(EntityInfo currentInfo)
        {
            //List<EntityError> errors = new List<EntityError>();
            //var breezeParticipants = TypedEntityInfo<ParticipantDto>.GetTyped(currentInfos)
            //    .ToLookup(p=>p.Info.EntityState);
            //var ids = breezeParticipants[b.EntityState.Modified].Select(p => p.Entity.Id);
            //var usrs = Context.Users.Where(u => ids.Contains(u.Id)).ToDictionary(u=>u.Id); 
            //realistically usually only 1 of these at a time
            var pMap = (ParticipantMaps)MapperConfig.GetMap<Participant, ParticipantDto>();
            var p = (ParticipantDto)currentInfo.Entity;
            Participant u;
            if (currentInfo.EntityState == b.EntityState.Modified)
            {
                u = Context.Users.AsNoTracking().Single(ui=>ui.Id == p.Id);
                Context.Entry(u).State = System.Data.Entity.EntityState.Detached;
                pMap.UpdateParticipant(u, p);
            }
            else
            {
                u = pMap.TypedMapToDomain(p);
                if (currentInfo.EntityState == b.EntityState.Added)
                {
                    object o = null;
                    currentInfo.UnmappedValuesMap?.TryGetValue("Password", out o);
                    IEnumerable<MappedEFEntityError> errs;
                    try
                    {
                        errs = CreateUser(u, (string)o)
                            .Select(e => MappedEFEntityError.Create(p, userValErrName, e, PropName(e))).ToList();
                    }
                    catch (DbEntityValidationException ex)
                    {
                        errs = MapErrors(ex, p);
                    }
                    if (errs.Any())
                    {
                        throw new EntityErrorsException(errs);
                    }
                }
            }
            var rv = currentInfo.ContextProvider.CreateEntityInfo(u, currentInfo.EntityState == b.EntityState.Added
                ? b.EntityState.Unchanged
                : currentInfo.EntityState);
            rv.OriginalValuesMap = currentInfo.OriginalValuesMap;
            rv.UnmappedValuesMap = currentInfo.UnmappedValuesMap;
            return rv;
        }
        const string userValErrName = "User Validation";
        static IEnumerable<MappedEFEntityError> MapErrors(DbEntityValidationException ex, ParticipantDto participant)
        {
            return ex.EntityValidationErrors.SelectMany(e => e.ValidationErrors)
                .Select(e => {
                    return MappedEFEntityError.Create(participant, userValErrName, e.ErrorMessage, PropName(e.PropertyName));
                });
        }

        static string PropName(string name)
        {
            int spacePos = name.IndexOf(' ');
            if (spacePos > -1)
            {
                name = name.Substring(0, spacePos);
            }
            return name.Equals("name", StringComparison.InvariantCultureIgnoreCase)
                        ? "UserName" : name;
        }

        IEnumerable<EntityError> GetCourseFormatErrors(List<EntityInfo> currentInfos)
        {
            var cfs = TypedEntityInfo<CourseFormatDto>.GetTyped(currentInfos);

            //multiple individual queries may be the way to go here
            //this query makes courseFormats courseType navigation property immutable - i.e. it cannot be changed once set
            var modified = cfs.Where(c => c.Info.EntityState == b.EntityState.Modified).ToList();
            if (modified.Any())
            {
                var pred = modified.Aggregate(PredicateBuilder.New<CourseFormat>(), (prev, cur) => prev.Or(
                c => cur.Entity.Id == c.Id &&
                    c.CourseTypeId != cur.Entity.CourseTypeId));
                if (Context.CourseFormats.Any(pred.Compile()))
                {
                    throw new InvalidDataException();
                }
            }
            
            var ids = cfs.Select(cf => cf.Entity.Id).ToList();
            var courseTypeIds = cfs.Select(cf => cf.Entity.CourseTypeId);

            var newFormatsForType = (from c in Context.CourseFormats
                                     where courseTypeIds.Contains(c.CourseTypeId) && !ids.Contains(c.Id)
                                     select new { c.Id, c.Description }).ToList();

            newFormatsForType.AddRange(cfs.Select(c => new { c.Entity.Id, c.Entity.Description }));

            return (from c in newFormatsForType
                    group c by c.Description into cg
                    where cg.Count() > 1
                    select cg).SelectMany(i => i)
                    .Where(i => ids.Contains(i.Id))
                    .Select(i => MappedEFEntityError.Create(cfs.First(ci => ci.Entity.Id == i.Id).Entity,
                        "RepeatWithinGroup",
                        string.Format("Each course format description must be unique within course type. [{0}]", i.Description),
                        "Description")).ToList(); //tolist so it throws here if a problem
        }

        IEnumerable<EntityError> GetParticipantErrors(List<EntityInfo> currentInfos)
        {
            var ps = TypedEntityInfo<ParticipantDto>.GetTyped(currentInfos);

            /* too dificult, and there are exceptions - had been trying to keep drs as drs etc
            var pred = PredicateBuilder.False<ProfessionalRole>();
            foreach (var p in ps)
            {
                object pr;
                if (p.Info.OriginalValuesMap.TryGetValue("ProfessionalRoleId", out pr) && !p.Entity.DefaultProfessionalRoleId.Equals(pr))
                {
                    Context.ProfessionalRoles.;
                }
            }
            */
            List<EntityError> returnVar = new List<EntityError>();

            foreach (var p in ps)
            {
                //if the user being modified does not have administrator priveleges for the new department we will have to remove 
                //the administrator priveleges after successful save - otherwise would provide a back door in to change departments for which we
                //should not have permission
                if (p.Entity.DefaultDepartmentId != Context.Users.Find(p.Entity.Id).DefaultDepartmentId)
                {
                    var originalDepartmentAccess = CurrentUser.GetDepartmentAdminIdsForUser(p.Entity.Id);
                    if (!originalDepartmentAccess.Contains(p.Entity.DefaultDepartmentId))
                    {
                        Guid userId = p.Entity.Id;
#pragma warning disable IDE0039 // Use local function
                        Action pred = () =>
                        {
#pragma warning restore IDE0039 // Use local function
                            var toRemove = Context.UserRoles.Where(ur => ur.UserId == userId);
                            Context.UserRoles.RemoveRange(toRemove);
                        };
                        p.Info.UnmappedValuesMap.Add(_afterUpdateDelegate, pred);
                    }
                }
            }
            return returnVar;
        }

        /*
         * use database unique constraint for this
            var dup = (from u in Context.Users
                       where p.Entity.Id != u.Id &&
                           p.Entity.FullName == u.FullName &&
                           p.Entity.DefaultDepartmentId == u.DefaultDepartmentId
                       select u.DefaultProfessionalRoleId).FirstOrDefault();
            if (dup != default(Guid) 
                && ((dup == p.Entity.DefaultProfessionalRoleId 
                    || (from r in Context.ProfessionalRoles
                        where (new[] { dup, p.Entity.DefaultProfessionalRoleId}).Contains(r.Id)
                        group r by r.Description into c
                        select c).Count() == 1)))
            { 
                returnVar.Add(MappedEFEntityError.Create(p.Entity,
                    "DuplicateUser",
                    "2 users with the same name, department and profession",
                    "FullName"));
            }

        }
        */

        IEnumerable<EntityError> GetInstitutionErrors(List<EntityInfo> currentInfos)
        {
            var insts = TypedEntityInfo<InstitutionDto>.GetTyped(currentInfos);
            List<EntityError> returnVar = new List<EntityError>();

            foreach (var i in insts)
            {
                try
                {
                    var ci = CultureInfo.GetCultureInfo(i.Entity.LocaleCode);
                    //not great separation of concerns here- this is not a buisness logic problem
                    if (i.Info.EntityState == b.EntityState.Added && !Context.Cultures.Any(c => c.LocaleCode == ci.Name))
                    {
                        CreateCulture(ci);
                    }
                }
                catch (CultureNotFoundException)
                {
                    returnVar.Add(MappedEFEntityError.Create(i.Entity,
                        "UnknownLocale",
                        "The Locale Code specified is not valid",
                        "LocaleCode"));
                }
                if (!string.IsNullOrWhiteSpace(i.Entity.StandardTimeZone))
                {
                    try
                    {
                        TimeZoneInfo.FindSystemTimeZoneById(i.Entity.StandardTimeZone);
                    }
                    catch (TimeZoneNotFoundException)
                    {
                        returnVar.Add(MappedEFEntityError.Create(i.Entity,
                            "UnknownTimeZone",
                            "The Time Zone specified is not valid",
                            "StandardTimeZone"));
                    }
                }
                if (!string.IsNullOrEmpty(i.Entity.HomepageUrl) && !WebValidation.IsAccessible(i.Entity.HomepageUrl))
                {
                    returnVar.Add(MappedEFEntityError.Create(i.Entity,
                        "InvalidWebPage",
                        "The web page cannot be found",
                        "HomepageUrl"));
                }
                returnVar.AddRange(GetFileErrors(i.Entity.File, i, () => Context.Institutions.Find(i.Entity.Id)));
            }
            return returnVar;
        }

        IEnumerable<EntityError> GetCandidatePrereadingErrors(List<EntityInfo> currentInfos)
        {
            var prereadings = TypedEntityInfo<CandidatePrereadingDto>.GetTyped(currentInfos);
            List<EntityError> returnVar = new List<EntityError>();

            foreach (var p in prereadings)
            {
                returnVar.AddRange(GetFileErrors(p.Entity.File, p, () => Context.CandidatePrereadings.Find(p.Entity.Id)));
            }
            return returnVar;
        }

        IEnumerable<EntityError> GetRoomErrors(List<EntityInfo> currentInfos)
        {
            var rooms = TypedEntityInfo<RoomDto>.GetTyped(currentInfos);
            List<EntityError> returnVar = new List<EntityError>();

            foreach (var r in rooms)
            {
                returnVar.AddRange(GetFileErrors(r.Entity.File, r, () => Context.Rooms.Find(r.Entity.Id)));
            }
            return returnVar;
        }

        IEnumerable<EntityError> GetScenarioResourceErrors(List<EntityInfo> currentInfos)
        {
            var resources = TypedEntityInfo<ScenarioResourceDto>.GetTyped(currentInfos);
            List<EntityError> returnVar = new List<EntityError>();

            foreach (var r in resources)
            {
                returnVar.AddRange(GetFileErrors(r.Entity.File, r, () => Context.ScenarioResources.Find(r.Entity.Id)));
            }
            return returnVar;
        }

        IEnumerable<EntityError> GetActivityErrors(List<EntityInfo> currentInfos)
        {
            var activities = TypedEntityInfo<ActivityDto>.GetTyped(currentInfos);
            List<EntityError> returnVar = new List<EntityError>();

            foreach (var a in activities)
            {
                returnVar.AddRange(GetFileErrors(a.Entity.File, a, () => Context.Activities.Find(a.Entity.Id)));
            }
            return returnVar;
        }

        IEnumerable<EntityError> GetFileErrors<T>(byte[] file, TypedEntityInfo<T> entityInfo, Func<IAssociateFileRequired> getExistingEntity) where T : class, IAssociateFileRequired
        {
            var returnVar = GetBaseFileErrors(file, entityInfo, entityInfo.Entity.FileSize, entityInfo.Entity.FileModified, () => getExistingEntity().AsOptional());
            if (entityInfo.Info.EntityState == b.EntityState.Added)
            {
                if (file == null)
                {
                    returnVar.Add(MappedEFEntityError.Create(entityInfo.Entity,
                        "FileNull",
                        "A file must be supplied for upload",
                        "File"));
                }
            }
            
            return returnVar;
        }

        List<EntityError> GetBaseFileErrors<T>(byte[] file, TypedEntityInfo<T> entityInfo, long? fileSize, DateTime? fileModified, Func<IAssociateFileOptional> getExistingEntity) where T:class, IAssociateFile
        {
            var returnVar = new List<EntityError>();
            if (file != null && fileSize != file.Length)
            {
                returnVar.Add(MappedEFEntityError.Create(entityInfo.Entity,
                    "FileSizeDifference",
                    "The file size stated is different to the size of the file being uploaded",
                    "FileSize"));
            }
            if (fileModified == default(DateTime))
            {
                returnVar.Add(MappedEFEntityError.Create(entityInfo.Entity,
                    "FileModifiedDefaultDate",
                    $"The file modified must not be the default value for a date ({(default(DateTime)):D})",
                    "FileModified"));
            }
            if (entityInfo.Info.EntityState == b.EntityState.Modified && file == null)
            {
                var existingEntity = getExistingEntity();
                if (entityInfo.Entity.FileName != null && entityInfo.Entity.FileName != existingEntity.FileName)
                {
                    returnVar.Add(MappedEFEntityError.Create(entityInfo.Entity,
                    "FileNameDifferWithExisting",
                    "The filename is different to the existing filename, but no new file is being uploaded",
                    "FileName"));
                }
                if (fileModified.HasValue && fileModified != existingEntity.FileModified)
                {
                    returnVar.Add(MappedEFEntityError.Create(entityInfo.Entity,
                    "FileModifiedDifferWithExisting",
                    "The file modified date is different to the existing date modified, but no new file is being uploaded",
                    "FileModified"));
                }
                if (fileSize.HasValue && fileSize != existingEntity.FileSize)
                {
                    returnVar.Add(MappedEFEntityError.Create(entityInfo.Entity,
                    "FileSizeDifferWithExisting",
                    "The file size is different to the existing file size, but no new file is being uploaded",
                    "FileSize"));
                }
            }
            return returnVar;
        }

        IEnumerable<EntityError> GetFileErrors<T>(byte[] file, TypedEntityInfo<T> entityInfo, Func<IAssociateFileOptional> getExistingEntity) where T: class, IAssociateFileOptional
        {
            var returnVar = GetBaseFileErrors(file, entityInfo, entityInfo.Entity.FileSize, entityInfo.Entity.FileModified, getExistingEntity);

            if ((entityInfo.Entity.FileModified == null) != (entityInfo.Entity.FileName == null) || (entityInfo.Entity.FileName == null) != (entityInfo.Entity.FileSize == null))
            {
                if (entityInfo.Entity.FileName == null)
                {
                    returnVar.Add(MappedEFEntityError.Create(entityInfo.Entity,
                        "FileNamePropertiesDiscordant",
                        "Filename is null but file size and date modified are not also null",
                        "FileName"));
                }
                else
                {
                    returnVar.Add(MappedEFEntityError.Create(entityInfo.Entity,
                        "FileNamePropertiesDiscordant",
                        "Filename is not null but file size or date modified are null",
                        "FileName"));
                }
            }
            return returnVar;
        }

        //if corseslots altered, need to update upcoming courses
        void UpdateICourseDays(IEnumerable<CourseSlot> alteredSlots)
        {
            var courseFormatIds = alteredSlots.Select(c => c.CourseFormatId).Distinct().ToList();
            var slotDays = Context.CourseSlots
                            .Where(cs => courseFormatIds.Contains(cs.CourseFormatId) && cs.IsActive)
                            .OrderBy(cs=>cs.Order)
                            .ToLookup(cs=>cs.Day,cs=>cs);

            foreach (var course in Context.Courses.Include("CourseDays").Include("CourseFormat")
                    .Where(c=>c.StartFacultyUtc > DateTime.UtcNow && courseFormatIds.Contains(c.CourseFormatId)))
            {
                var days = course.AllDays().ToDictionary(k=>k.Day);

                for (int i = 1; i <= course.CourseFormat.DaysDuration; i++)
                {
                    if (!days.TryGetValue(i, out ICourseDay icd))
                    {
                        icd = new CourseDay
                        {
                            Day = i,
                            Course = course,
                            StartFacultyUtc = days[i - 1].StartFacultyUtc
                        };
                        Context.CourseDays.Add((CourseDay)icd);
                        days.Add(i, icd);
                    }
                    var slots = slotDays[(byte)i] ?? Enumerable.Empty<CourseSlot>();
                    icd.DurationFacultyMins = slots.Sum(s=>s.MinutesDuration);
                    icd.DelayStartParticipantMins = slots.TakeWhile(s => s.FacultyOnly).Sum(s => s.MinutesDuration);
                    var minsFromParticipantEnd = slots.Reverse().TakeWhile(s => s.FacultyOnly).Sum(s => s.MinutesDuration);
                    icd.DurationParticipantMins = icd.DurationFacultyMins - icd.DelayStartParticipantMins - minsFromParticipantEnd;
                }
                foreach (var k in days.Keys.Where(d=> d > course.CourseFormat.DaysDuration))
                {
                    days[k].DurationFacultyMins = days[k].DurationParticipantMins = 0;
                }
            }
            Context.SaveChanges();
        }
        //not great separation of concerns here- this is not a buisness logic problem 
        /*
        IEnumerable<MappedEFEntityError> GetCourseSlotErrors(List<EntityInfo> currentInfos)
        {
            var insts = TypedEntityinfo<CourseSlot>.GetTyped(currentInfos);

            foreach (var i in insts)
            {
                object wasActive;
                if (!i.Entity.IsActive && i.Info.OriginalValuesMap.TryGetValue("IsActive", out wasActive) 
                    && (bool)wasActive)
                {
                    //could check all collections, but probably easiest to have a crack and see how we go
                    try
                    {
                        //need to do this as Participant of update
                    }
                }
            }
        }
        */

        private void CreateCulture(CultureInfo ci)
        {
            var ri = new RegionInfo(ci.LCID);
            var iso = ISO3166.FromAlpha2(ri.TwoLetterISORegionName);
            var c = new Culture
            {
                Name = ci.DisplayName,
                LocaleCode = ci.Name,
                CountryCode = iso.NumericCode,
                DialCode = iso.DialCodes.FirstOrDefault()
            };
            Context.Cultures.Add(c);
            Context.SaveChanges();
        }

        internal IEnumerable<BookingChangeDetails> GetBookingChanges(Dictionary<Type, List<EntityInfo>> saveMap)
        {
            //in reality, never going to be adding or updating more than 1 course at a time
            var bcd = new BookingChangeDetails();
            IEnumerable<Guid> allRoomIds = new Guid[0];
            IEnumerable<Guid> manikinIds = new Guid[0];
#pragma warning disable IDE0018 // Inline variable declaration
            List<EntityInfo> ei;
#pragma warning restore IDE0018 // Inline variable declaration
            if (saveMap.TryGetValue(typeof(Course), out ei))
            {
                
                var cs = TypedEntityInfo<Course>.GetTyped(ei).ToLookup(c => c.Info.EntityState);
                object roomId = null;
                var addedRoomIds = cs[b.EntityState.Added]
                    .Concat(cs[b.EntityState.Modified].Where(m => m.Info.OriginalValuesMap.TryGetValue(nameof(m.Entity.RoomId), out roomId) &&
                        !m.Entity.RoomId.Equals(Guid.Parse((string)roomId))))
                    .ToHashSet(c => c.Entity.RoomId);

                var removedRoomIds = cs[b.EntityState.Deleted].ToHashSet(c => c.Entity.RoomId)
                    .AddRange(from c in cs[b.EntityState.Modified]
                              where c.Info.OriginalValuesMap.TryGetValue(nameof(c.Entity.RoomId), out roomId) && !c.Entity.RoomId.Equals(roomId)
                              select Guid.Parse((string)roomId));

                allRoomIds = addedRoomIds.Union(removedRoomIds);
                var rooms = Context.Rooms.Where(r => allRoomIds.Contains(r.Id)).ToList();

                bcd.AddedRoomBooking = rooms.FirstOrDefault(r => addedRoomIds.Contains(r.Id));
                bcd.RemovedRoomBooking = rooms.FirstOrDefault(r => removedRoomIds.Contains(r.Id));
                bcd.RelevantCourse = cs.Single().Single().Entity; //hmmm - this will actually be attached to a different entity manager
            }
            if (saveMap.TryGetValue(typeof(CourseSlotManikin), out ei))
            {
                //get manikins for which currentUser is not the manikin booking admin
                var csm = TypedEntityInfo<CourseSlotManikin>.GetTyped(ei);
                manikinIds = csm.Select(e => e.Entity.ManikinId);
                var manikins = Context.Manikins.Where(m => manikinIds.Contains(m.Id))
                    .ToDictionary(m => m.Id);
                var entityStateMans = csm.ToLookup(k => k.Info.EntityState, v => manikins[v.Entity.ManikinId]);

                bcd.AddedManikinBookings = entityStateMans[b.EntityState.Added].Except(entityStateMans[b.EntityState.Deleted]).ToList();
                bcd.RemovedManikinBookings = entityStateMans[b.EntityState.Deleted].Except(entityStateMans[b.EntityState.Added]).ToList();
                if (bcd.RelevantCourse == null)
                {
                    bcd.RelevantCourse = Context.Courses.Find(csm.First().Entity.CourseId);
                }
            }
            if (bcd.AddedRoomBooking != null || bcd.RemovedRoomBooking != null 
                || (bcd.AddedManikinBookings!=null && bcd.AddedManikinBookings.Any())
                || (bcd.RemovedManikinBookings != null && bcd.RemovedManikinBookings.Any()))
            {
                var manikinAdminId = (from r in RoleConstants.RoleNames where r.Value == RoleConstants.DptManikinBookings select r.Key).First();
                var roomAdminId = (from r in RoleConstants.RoleNames where r.Value == RoleConstants.DptRoomBookings select r.Key).First();
                var manikinAndRoomAdmins = Context.Users.Include("Department.Institution.Culture").Include(u=>u.Roles)
                    .Where(u => (u.Roles.Any(r => r.RoleId == manikinAdminId)
                            && u.Department.Manikins.Any(m => manikinIds.Contains(m.Id)))
                        || (u.Roles.Any(r => r.RoleId == roomAdminId)
                            && u.Department.Rooms.Any(r => allRoomIds.Contains(r.Id))))
                    .ToLookup(u=> new { DptId = u.DefaultDepartmentId, ManikinAdmin = u.Roles.Any(r=>r.RoleId==manikinAdminId), RoomAdmin = u.Roles.Any(r => r.RoleId == roomAdminId) }, u=>u);
                return manikinAndRoomAdmins.Select(ma=> {
                    var returnVar = new BookingChangeDetails
                    {
                        PersonBooking = CurrentUser.Principal,
                        Notify = ma,
                        RelevantCourse = bcd.RelevantCourse,
                    };
                    if (ma.Key.ManikinAdmin)
                    {
                        returnVar.AddedManikinBookings = bcd.AddedManikinBookings.Where(m => m.DepartmentId == ma.Key.DptId).ToList();
                        returnVar.RemovedManikinBookings = bcd.RemovedManikinBookings.Where(m => m.DepartmentId == ma.Key.DptId).ToList();
                    }
                    if (ma.Key.RoomAdmin)
                    {
                        returnVar.AddedRoomBooking = bcd.AddedRoomBooking?.DepartmentId == ma.Key.DptId ? bcd.AddedRoomBooking : null;
                        returnVar.RemovedRoomBooking = bcd.RemovedRoomBooking?.DepartmentId == ma.Key.DptId ? bcd.RemovedRoomBooking : null;
                    }
                    return returnVar;
                }).ToList();
            }
            return new BookingChangeDetails[0];
        }

        [Serializable]
        public class InvalidDataException : Exception
        {
            public InvalidDataException() : base() { }
            public InvalidDataException(string msg) : base(msg) { }
        }

        class TypedEntityInfo<T>
        {
            internal T Entity;
            internal EntityInfo Info;

            internal static IEnumerable<TypedEntityInfo<T>> GetTyped(IEnumerable<EntityInfo> info)
            {
                return info.Select(i => new TypedEntityInfo<T> { Info = i, Entity = (T)i.Entity }).ToList();
            }
        }

        class MappedEFEntityError : EntityError
        {
            /// <summary>
            /// 
            /// </summary>
            /// <param name="entityInfo"></param>
            /// <param name="errorName"></param>
            /// <param name="errorMessage"></param>
            /// <param name="propertyName"></param>
            /// <param name="dtoType">If not specified, the TypeName from</param>
            internal MappedEFEntityError() { }
            public static MappedEFEntityError Create<T>(T entity, string errorName, string errorMessage, string propertyName)
            {
                return new MappedEFEntityError
                {
                    EntityTypeName = typeof(T).FullName,
                    KeyValues = GetKeyValues(entity),

                    ErrorName = errorName,
                    ErrorMessage = errorMessage,
                    PropertyName = propertyName,
                };
            }

            private static object[] GetKeyValues<T>(T entity)
            {
                //all primary key mapping is on the metadata type - could argue we should somehow be working on a version of the ContextPretender here!

                Type metaTypeFromAttr = ((MetadataTypeAttribute)typeof(T).GetCustomAttributes(typeof(MetadataTypeAttribute), false).Single()).MetadataClassType;
                var keyProps = metaTypeFromAttr.GetProperties().Where(prop => Attribute.IsDefined(prop, typeof(KeyAttribute)))
                    .Select(vp=>typeof(T).GetProperty(vp.Name)).ToList();
                if (keyProps.Count == 1)
                {
                    return new[] { keyProps[0].GetValue(entity) };
                }
                return keyProps.Select(kp => new {
                    value = kp.GetValue(entity),
                    order = kp.GetCustomAttributes(typeof(ColumnAttribute), true).Cast<ColumnAttribute>().Select(ca => ca.Order)
                }).OrderBy(a => a.order).Select(a => a.value).ToArray();

            }
        }
    }
}
