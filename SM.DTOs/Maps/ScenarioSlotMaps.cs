using SM.DataAccess;
using SM.Dto;
using System;
using System.Linq.Expressions;
namespace SM.Dto.Maps
{
    internal static class ScenarioSlotMaps
    {
        internal static Func<ScenarioSlotDto, ScenarioSlot> mapToRepo()
        {
            return m => new ScenarioSlot
            {
                Id = m.Id,
                MinutesDuration = m.MinutesDuration,
                Day = m.Day,
                Order = m.Order
                //CourseTypes = m.CourseTypes
            };
        }

        internal static Expression<Func<ScenarioSlot, ScenarioSlotDto>> mapFromRepo()
        {
            return m => new ScenarioSlotDto
            {
                Id = m.Id,
                MinutesDuration = m.MinutesDuration,
                Day = m.Day,
                Order = m.Order
                //CourseTypes = m.CourseTypes
            };
        }
    }
}
