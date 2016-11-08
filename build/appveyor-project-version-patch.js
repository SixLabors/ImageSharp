var jsonfile = require("jsonfile");
var semver = require("semver");

var file = "../src/imagesharp/project.json";

var semversion = semver.valid(process.env.mssemver);

jsonfile.readFile(file, function (err, project) {
	project.version = semversion;
	jsonfile.writeFile(file, project, {spaces: 2}, function(err) {
		console.error(err);
	});
})