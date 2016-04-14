(function () {
    'use strict';

    // Define the common module 
    // Contains services:
    //  - common
    //  - logger
    //  - spinner
    var commonModule = angular.module('common', []);

    // Must configure the common service and set its 
    // events via the commonConfigProvider
    commonModule.provider('commonConfig', function () {
        this.config = {
            // These are the properties we need to set
            //controllerActivateSuccessEvent: '',
            //spinnerToggleEvent: ''
        };

        this.$get = function () {
            return {
                config: this.config
            };
        };
    });

    commonModule.factory('common',
        ['$q', '$rootScope', '$timeout', 'commonConfig', 'logger', /* 'dateUtilities', */common]);

    function common($q, $rootScope, $timeout, commonConfig, logger /*, dateUtilities */) {
        var throttles = {};

        var service = {
            // common angular dependencies
            $broadcast: $broadcast,
            $timeout: $timeout,
            // generic
            activateController: activateController,
            createSearchThrottle: createSearchThrottle,
            debouncedThrottle: debouncedThrottle,
            getEnumValues: window.medsimMetadata.getEnums,
            getRoleIcon: getRoleIcon,
            isEmptyObject: isEmptyObject,
            isNumber: isNumber,
            logger: logger, // for accessibility
            //dateUtilities: dateUtilities,  // for accessibility
            sortOnPropertyName: sortOnPropertyName,
            sortOnChildPropertyName: sortOnChildPropertyName,
            
            textContains: textContains,
            toSeperateWords: toSeperateWords
        };

        return service;

        function activateController(promises, controllerId) {
            return $q.all(promises).then(function (eventArgs) {
                var data = { controllerId: controllerId };
                $broadcast(commonConfig.config.controllerActivateSuccessEvent, data);
            });
        }

        function $broadcast() {
            return $rootScope.$broadcast.apply($rootScope, arguments);
        }

        function createSearchThrottle(viewmodel, list, filteredList, filter, delay) {
            // After a delay, search a viewmodel's list using 
            // a filter function, and return a filteredList.

            // custom delay or use default
            delay = +delay || 300;
            // if only vm and list parameters were passed, set others by naming convention 
            if (!filteredList) {
                // assuming list is named sessions, filteredList is filteredSessions
                filteredList = 'filtered' + list[0].toUpperCase() + list.substr(1).toLowerCase(); // string
                // filter function is named sessionFilter
                filter = list + 'Filter'; // function in string form
            }

            // create the filtering function we will call from here
            var filterFn = function () {
                // translates to ...
                // vm.filteredSessions 
                //      = vm.sessions.filter(function(item( { returns vm.sessionFilter (item) } );
                viewmodel[filteredList] = viewmodel[list].filter(function(item) {
                    return viewmodel[filter](item);
                });
            };

            return (function () {
                // Wrapped in outer IFFE so we can use closure 
                // over filterInputTimeout which references the timeout
                var filterInputTimeout;

                // return what becomes the 'applyFilter' function in the controller
                return function(searchNow) {
                    if (filterInputTimeout) {
                        $timeout.cancel(filterInputTimeout);
                        filterInputTimeout = null;
                    }
                    if (searchNow || !delay) {
                        filterFn();
                    } else {
                        filterInputTimeout = $timeout(filterFn, delay);
                    }
                };
            })();
        }

        function debouncedThrottle(key, callback, delay, immediate) {
            // Perform some action (callback) after a delay. 
            // Track the callback by key, so if the same callback 
            // is issued again, restart the delay.

            var defaultDelay = 1000;
            delay = delay || defaultDelay;
            if (throttles[key]) {
                $timeout.cancel(throttles[key]);
                throttles[key] = undefined;
            }
            if (immediate) {
                callback();
            } else {
                throttles[key] = $timeout(callback, delay);
            }
        }

        function isNumber(val) {
            // negative or positive
            return /^[-]?\d+$/.test(val);
        }

        function isEmptyObject(val) {
            for (var p in val) {
                return false;
            }
            return true;
        }

        function textContains(text, searchText) {
            return text && -1 !== text.toLowerCase().indexOf(searchText.toLowerCase());
        }

        function toSeperateWords(text) {
            return text.replace(/([A-Z])/g,"  $1").trimLeft();
        }

        function sortOnPropertyName(propName) {
            return function (a, b) {
                if (a[propName] > b[propName]) {
                    return 1;
                }
                if (a[propName] < b[propName]) {
                    return -1;
                }
                // a must be equal to b
                return 0;
            }
        }

        function sortOnChildPropertyName(propName, childPropName) {
            return function (a, b) {
                if (a[propName][childPropName] > b[propName][childPropName]) {
                    return 1;
                }
                if (a[propName][childPropName] < b[propName][childPropName]) {
                    return -1;
                }
                // a must be equal to b
                return 0;
            }
        }

        var _roleIcons;
        function getRoleIcon(roleName) {
            if (!_roleIcons) {
                var symbols = {
                    Medical: 'stethoscope',
                    Tech: 'wrench',
                    Perfusionist: 'cog',
                    Other: 'question',
                    Paramedic: 'ambulance',
                    Nursing: 'heartbeat'
                }
                _roleIcons = {};
                angular.forEach(symbols, function (val,key) {
                    _roleIcons[key] = "fa fa-" + val;
                });
            }
            return _roleIcons[roleName];
        }
    }
})();