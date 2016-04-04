(function() {
    'use strict';

    var serviceId = 'controller.abstract';
    angular.module('app').factory(serviceId,
        ['common', '$window', 'commonConfig', 'breeze', AbstractRepository]);

    function AbstractRepository(common, $window, commonConfig, breeze) {
        var confirmDiscardMsg = 'Are you sure you want to discard changes without saving?';

        //var provider = entityManagerFactory.manager;

        function Ctor(argObj /* controllerId, $scope, watchedEntityName*/) {
            var vm = this;
            var hasAddedEntityPropertyChanged = false;

            var $on = argObj.$scope.$on.bind(argObj.$scope);
            var unwatchers = [$on('$destroy', destroy)]; 

            if (argObj.$scope.asideInstance) {
                vm.close = modalClose;
            } else {
                unwatchers.push($on('$routeChangeStart', beforeRouteChange));
            }

            if (argObj.$watches) {unwatchers = unwatchers.concat(argObj.$watches);}
            $window.addEventListener("beforeunload", beforeUnload);

            vm.log = common.logger.getLogFn(argObj.controllerId);
            vm.disableSave = disableSave;
            vm.disableSaveInclChildren = disableSaveInclChildren;

            function hasDataChangedInWatchedOrChildren(){
                return getEntAndChildrenArray().filter(hasChangedEnt);

                function hasChangedEnt(ent) {
                    switch (ent.entityAspect.entityState) {
                        case breeze.EntityState.Modified:
                        case breeze.EntityState.Deleted:
                            return true;
                        case breeze.EntityState.Added:
                            //single keys autogenerated
                            var isSingleKey = ent.entityType.keyProperties.length === 1;
                            return !ent.entityType.dataProperties.every(function (dp) {
                                return (isSingleKey && dp.isPartOfKey) || ent[dp.name] === dp.defaultValue;
                            });
                        default:
                            return false;
                    }
                }
            }

            function beforeUnload(e){
                if (hasDataChangedInWatchedOrChildren().length) {
                    e.returnValue = confirmDiscardMsg; // Gecko, Trident, Chrome 34+
                    return confirmDiscardMsg;          // Gecko, WebKit, Chrome <34
                }
            }

            function beforeRouteChange(e) {
                if (!e.defaultPrevented) {
                    var changed = hasDataChangedInWatchedOrChildren();
                    if (changed.length && !confirm(confirmDiscardMsg)) {
                        e.preventDefault();
                        common.$broadcast(commonConfig.config.controllerActivateSuccessEvent); //switch the spinner off
                    } else {
                        changed.forEach(function (ent) {
                            ent.entityAspect.rejectChanges();
                        });
                        
                        destroy({}); //note this will remove listeners on the hide event, but as the controller has a new controller injected ever time
                        //, this will do for now
                    }
                }
            }


            function destroy(e) {
                if (unwatchers && !e.defaultPrevented) {
                    $window.removeEventListener("beforeunload", beforeUnload);
                    unwatchers.forEach(function (unwatch) {
                        unwatch();
                    });
                    unwatchers = null;
                }
            }

            function disableSave(ent) {
                ent = ent || vm[argObj.watchedEntityName];
                if (ent && ent.entityAspect) {
                    return !ent.entityAspect.entityState.isAddedModifiedOrDeleted()
                        || ent.entityAspect.hasValidationErrors;
                }
                return true;
            }

            function getEntAndChildrenArray() {
                var ent = vm[argObj.watchedEntityName];
                if (ent && ent.entityAspect) {
                    var returnVar = [ent];
                    ent.entityType.navigationProperties.forEach(function(np){
                        if (!np.isScalar){
                            returnVar = returnVar.concat(ent[np.name]);

                            returnVar = returnVar.concat(ent.entityAspect.entityManager.getEntities(np.entityType, breeze.EntityState.Deleted)
                                .filter(function (deletedEnt) {
                                    for (var i = 0; i < np.invForeignKeyNames.length; i++) {
                                        if (deletedEnt[np.invForeignKeyNames[i]] !== ent[ent.entityType.keyProperties[i].name]) {
                                            return false;
                                        }
                                    }
                                    return true;
                                }));
                         }
                        //
                    });
                    
                    return returnVar;
                }
                return [];
            }

            function disableSaveInclChildren() {
                var entArray = getEntAndChildrenArray().map(function (el) {
                    return el.entityAspect;
                });
                return entArray.some(function (ea) {
                        return ea.isBeingSaved || ea.hasValidationErrors;
                    }) || !entArray.some(function (ea) {
                        return ea.entityState.isAddedModifiedOrDeleted();
                    });
            }


            function modalClose() {
                var evtArg = {
                    defaultPrevented: false,
                    preventDefault: function () {
                        this.defaultPrevented = true;
                    }
                };
                beforeRouteChange(evtArg);
                if (!evtArg.defaultPrevented) {
                    argObj.$scope.asideInstance.hide();
                    destroy(evtArg);
                }
            }
        }

        //no point instantiating above (as true factory method) as will only extend other methods
        Ctor /* .prototype */ = { 
            constructor: Ctor,
        };

        return Ctor;

        /* Implementation */
        /*
        function setIsPartialTrue(entities) {
            // call for all "partial queries"
            for (var i = entities.length; i--;) { entities[i].isPartial = true; }
            return entities;
        }
        */
    }
})();