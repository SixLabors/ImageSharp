var jsonfile = require('jsonfile');
var semver = require('semver');

var file = '../src/imageprocessor/project.json';
var buildVersion = process.env.APPVEYOR_BUILD_VERSION.substring(1);
//var buildVersion = '3.0.0.23';

var findPoint       = buildVersion.lastIndexOf(".");
var basePackageVer  = buildVersion.substring(0, findPoint);
var buildNumber     = buildVersion.substring(findPoint + 1, buildVersion.length);
var semversion 		= semver.valid(basePackageVer + '-alpha-' + buildNumber)

jsonfile.readFile(file, function (err, project) {
	project.version = semversion;
	jsonfile.writeFile(file, project, {spaces: 2}, function(err) {
		console.error(err);
	});
})