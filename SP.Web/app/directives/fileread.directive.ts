﻿import angular from 'angular';
//http://stackoverflow.com/questions/17063000/ng-model-for-input-type-file/17063046#17063046
export default angular.module('app').directive('appFilereader', ['$q', function ($q) {
    return {
        restrict: 'A',
        require: '?ngModel',
        scope: {
            ngLastModified: '=',
            ngFileSize: '=',
            ngFileName: '='
        },
        link: function(scope:any, element, _attrs, ngModel: any) {
            if (!ngModel) return;
            var stripData = /^data:[^;]*;base64,/;
            ngModel.$render = function () { };

            element.bind('change', function(e:any) {
                var file = e.target.files[0];
                scope.ngFileName = file.name;
                scope.ngLastModified = file.lastModified;
                scope.ngFileSize = file.size;
                readFile(file)
                    .then(function(values) {
                        ngModel.$setViewValue(values.length ? values.replace(stripData,'') : null);
                    });

                function readFile(file) {
                    var deferred = $q.defer();

                    var reader = new FileReader();
                    reader.onload = function(e:any) {
                        deferred.resolve(e.target.result);
                    };
                    reader.onerror = function(e) {
                        deferred.reject(e);
                    };
                    reader.readAsDataURL(file);

                    return deferred.promise;
                }

            }); //change
            scope.$on('$destory', function () { element.unbind("change"); });
        } //link
    }; //return
}]);