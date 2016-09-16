﻿(function () {
    'use strict';
    var controllerId = 'cultures';
    angular
        .module('app')
        .controller(controllerId, controller);

    controller.$inject = ['common', 'datacontext', '$q'];
    //changed $uibModalInstance to $scope to get the events

    function controller(common, datacontext, $q) {
        /* jshint validthis:true */
        var vm = this;
        var log = common.logger.getLogFn(controllerId);
        vm.cultures = [];

        activate();

        function activate() {
            var cultures;
            common.activateController([
                $q.all([datacontext.ready(),
                    datacontext.cultures.findServerIfCacheEmpty({ expand: ['institutions.departments.manequins', 'institutions.departments.scenarios'] }).then(function (data) {
                        cultures = data;
                    })]).then(function(){
                        var sortNameFn = common.sortOnPropertyName('name');
                        cultures.sort(sortNameFn);
                        cultures.forEach(institutionSort);
                        vm.cultures = cultures;

                        function dptItemsSort(d) {
                            d.manequins.sort(common.sortOnPropertyName('description'));
                            d.scenarios.sort(common.sortOnPropertyName('briefDescription'));
                            console.log(d.rooms.map(r=>r.shortDescription));
                            d.rooms.sort(common.sortOnPropertyName('shortDescription'));
                            console.log(d.rooms.map(r=>r.shortDescription));
                        }
                        function dptSort(i) {
                            i.departments.sort(sortNameFn);
                            i.departments.forEach(dptItemsSort)
                        };
                        function institutionSort(c) {
                            c.flagUrl = common.getFlagUrlFromLocaleCode(c.localeCode);
                            c.institutions.sort(sortNameFn);
                            c.institutions.forEach(dptSort);
                        };
                })], controllerId);
        }
    }

})();