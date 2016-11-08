var jsonfile = require("jsonfile");
var semver = require("semver");

var file = "../src/imagesharp/project.json";
var buildVersion = process.env.APPVEYOR_BUILD_VERSION.substring(1);

var findPoint = buildVersion.lastIndexOf(".");
var basePackageVer = buildVersion.substring(0, findPoint);
var buildNumber = buildVersion.substring(findPoint + 1, buildVersion.length);
var pad = "000000";
var paddedBuildNumber = pad.substring(0, pad.length - buildNumber.length) + buildNumber;
var semversion = semver.valid(basePackageVer + "-alpha" + paddedBuildNumber);

jsonfile.readFile(file, function (err, project) {
	project.version = semversion;
	jsonfile.writeFile(file, project, {spaces: 2}, function(err) {
		console.error(err);
	});
})