﻿(function () {
    'use strict';
    var controllerId = 'facultyInvites';
    angular
        .module('app')
        .controller(controllerId, controller);

    controller.$inject = ['common', '$scope', 'users.abstract', 'datacontext', 'tokenStorageService', 'breeze', '$http'];

    function controller(common, $scope, abstractUserDetails, datacontext, tokenStorageService, breeze, $http) {
        /* jshint validthis:true */
        var vm = this;
        var facultyLimitPred = [];
        var log = common.logger.getLogFn(controllerId);

        abstractUserDetails.constructor.call(this, $scope);
        vm.title = 'Faculty Invites';
        vm.addPerson = addPerson;
        vm.allCourseTypes = [];
        vm.completedCourseTypeId = null;
        vm.completedCourseTypeChange = completedCourseTypeChange;
        vm.courses = [];
        vm.courseTypesForPrincipal = [];
        vm.additionalFilters = filterChanged;

        vm.selectedCourseType = {};
        vm.selectedInvitees = [];
        vm.sendInvites = sendInvites;

        vm.gridOptions.columnDefs.push({
            name: 'Invite', field: 'invite',
            type: 'boolean',
            cellTemplate: '<div class="ui-grid-cell-contents"><button type="button" class="btn btn-sm" ng-click="grid.appScope.vm.addPerson(row.entity)">Invite</button></div>',
            enableSorting: false,
            enableFiltering: false,
            enableHiding: false
        });
        activate();

        function activate() {
            datacontext.ready().then(function () {
                var pred = breeze.Predicate.create('isOrganiser', '==', true)
                    .and(breeze.Predicate.create('participantId', '==', tokenStorageService.getUserId()));
                pred = breeze.Predicate.create('courseParticipants', 'any', pred)
                    .and(breeze.Predicate.create('startUtc', '>', new Date()));
                common.activateController([vm.baseReady,
                    datacontext.courseTypes.all().then(function (data) {
                        vm.allCourseTypes = data;
                    }),
                    datacontext.courses.find({ where: pred, expand: 'courseParticipants', orderBy: 'startUtc' }).then(function (data) {
                        vm.courses = data;
                        var cts = new Set(data.map(function (c) {
                            return c.courseFormat.courseType;
                        }));
                        vm.courseTypesForPrincipal = Array.from(cts);
                        if (vm.courseTypesForPrincipal.length) {
                            vm.selectedCourseType = vm.courseTypesForPrincipal[0];
                            vm.completedCourseTypeId = vm.selectedCourseType.instructorCourseId;
                        }
                        completedCourseTypeChange();
                    })], controllerId);
            });
        }

        function addPerson(person) {
            if (!vm.selectedInvitees.some(function (si) { return si.id === person.id; })) {
                vm.selectedInvitees.push(person);
            }
        }

        function filterChanged() {
            return facultyLimitPred;
        }

        function completedCourseTypeChange() {
            if (vm.completedCourseTypeId) {
                var pred = breeze.Predicate.create('course.courseFormat.courseTypeId', '==', vm.completedCourseTypeId)
                    .and('isConfirmed', '!=', 'false')
                    .and('course.startUtc', '<', new Date());
                facultyLimitPred = [breeze.Predicate.create('courseParticipants', 'any', pred)];
            } else {
                facultyLimitPred = [];
            }
            vm.filterChanged();
        }

        function sendInvites() {
            var data = {
                Invitees: vm.selectedInvitees.map(function (el) { return el.id; }),
                Courses: filterMap(vm.courses)
            };
            datacontext.save(vm.courses); //in case the number required has changed
            $http({
                method: 'POST',
                url: 'api/CoursePlanning/MultiInvite/',
                data:data
            }).then(function (response) {
                vm.selectedInvitees.length = 0;
                
                vm.courses.forEach(deleteInvite);
            }, log.error);
            function filterMap(arr) {
                var returnVar = [];
                arr.forEach(function (el) {
                    if (el.invite) {
                        returnVar.push(el.id);
                    }
                });
                return returnVar;
            }
            function deleteInvite(i) {
                delete i.invite;
            }
        }
    }
})();