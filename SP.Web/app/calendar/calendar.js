﻿(function () {
    'use strict';
    var controllerId = 'calendar';
    angular.module('app').controller(controllerId, ['moment', calendar]);

    function calendar(moment) {
        var vm = this;

        //These variables MUST be set as a minimum for the calendar to work
        vm.calendarView = 'month';
        vm.viewDate = new Date();
        vm.events = [
          {
              title: 'An event',
              type: 'warning',
              startsAt: moment().startOf('week').subtract(2, 'days').add(8, 'hours').toDate(),
              endsAt: moment().startOf('week').add(1, 'week').add(9, 'hours').toDate(),
              draggable: true,
              resizable: true
          }, {
              title: '<i class="glyphicon glyphicon-asterisk"></i> <span class="text-primary">Another event</span>, with a <i>html</i> title',
              type: 'info',
              startsAt: moment().subtract(1, 'day').toDate(),
              endsAt: moment().add(5, 'days').toDate(),
              draggable: true,
              resizable: true
          }, {
              title: 'This is a really long event title that occurs on every year',
              type: 'important',
              startsAt: moment().startOf('day').add(7, 'hours').toDate(),
              endsAt: moment().startOf('day').add(19, 'hours').toDate(),
              draggable: true,
              resizable: true
          }
        ];

        vm.isCellOpen = true;

        vm.eventClicked = function (event) {
            alert(event);
        };

        vm.timespanClicked = function (event) {
            alert(event);
        };

        vm.toggle = function ($event, field, event) {
            $event.preventDefault();
            $event.stopPropagation();
            event[field] = !event[field];
        };

        function mapCourse(course) {
            return {
                title: '<i class="glyphicon glyphicon-asterisk"></i> <span class="text-primary">Another event</span>, with a <i>html</i> title',
                type: course.department.abbrev,
                startsAt: course.startTime,
                endsAt: new Date(course.startTime + course.duration),
                draggable: true,
                resizable: true
            };
        }
    }
})();