<?PHP
include("databaseinfo.php");

$now = time();

//
// DB
//
mysql_connect ($DB_HOST, $DB_USER, $DB_PASSWORD);
mysql_select_db ($DB_NAME);

#
#  Copyright (c) Fly Man (http://opensimulator.org/)
#

###################### No user serviceable parts below #####################

#
# The XMLRPC server object
#

$xmlrpc_server = xmlrpc_server_create();

#
# Send email to the database
#

xmlrpc_server_register_method($xmlrpc_server, "send_email",
		"send_email");

function send_email($method_name, $params, $app_data)
{
	$req 			= $params[0];

	$from			= $req['fromaddress'];
	$to				= $req['toaddress'];
	$timestamp		= $req['timestamp'];
	$region			= $req['region'];
	$object			= $req['objectname'];
	$objectlocation	= $req['position'];
	$subject	 	= $req['subject'];
	$message		= $req['messagebody'];

	$result = mysql_query("INSERT INTO email VALUES('".mysql_escape_string($to)."','".
							mysql_escape_string($from)."',".
							mysql_escape_string($timestamp).",'".
							mysql_escape_string($region)."','".
							mysql_escape_string($object)."','".
							mysql_escape_string($objectlocation)."','".
							mysql_escape_string($subject)."','".
							mysql_escape_string($message)."')");
		
	$data = array();
	
	if (mysql_affected_rows() > 0)
	{
		$data[] = array(
				"saved" => "Yes"
				);
	}
	else
	{
		$data[] = array(
				"saved" => "No"
				);
	}

	
	$response_xml = xmlrpc_encode(array(
		'success'	  => True,
		'errorMessage' => "",
		'data' => $data
	));

	print $response_xml;
}

#
# Check if there's email in the database
#

xmlrpc_server_register_method($xmlrpc_server, "check_email",
		"check_email");

function check_email($method_name, $params, $app_data)
{
	$req 			= $params[0];

	$object			= $req['objectid'];

	$sql = "SELECT COUNT(*) as num FROM email WHERE `to` = '".mysql_escape_string($object)."'";

	$result = mysql_query($sql);

	$data = array();

	while (($row = mysql_fetch_assoc($result)))
	{
		$data[] = array(
			"num_emails" => $row['num']);
	}

	$response_xml = xmlrpc_encode(array(
		'success'	  => True,
		'errorMessage' => "",
		'data' => $data
	));

	print $response_xml;
}

#
# Retrieve messages from the database
#

xmlrpc_server_register_method($xmlrpc_server, "retrieve_email",
		"retrieve_email");

function retrieve_email($method_name, $params, $app_data)
{
	$req 			= $params[0];

	$object			= $req['objectid'];
	$rows			= $req['number'];

	$sql = "SELECT `timestamp`,`subject`, `from`,`objectname`,`region`,`objectlocation`,`message` FROM email WHERE `to` = '".mysql_escape_string($object)."' LIMIT 0,".$rows;

	$result = mysql_query($sql);
	
	$data = array();
	while ($row = mysql_fetch_assoc($result))
	{
		$data[] = array(
				"timestamp" => $row["timestamp"],
				"subject" => $row["subject"],
				"sender" => $row["from"],
				"objectname" => $row["objectname"],
				"region" => $row["region"],
				"objectpos" => $row["objectlocation"],
				"message" => $row["message"]);
	}

	// Now delete the email from the database

	$delete = "DELETE FROM email WHERE `to` = '".mysql_escape_string($object)."'";

	$result = mysql_query($delete);

	$response_xml = xmlrpc_encode(array(
		'success'	  => True,
		'errorMessage' => "",
		'data' => $data
	));

	print $response_xml;
}

#
# Process the request
#

$request_xml = $HTTP_RAW_POST_DATA;
xmlrpc_server_call_method($xmlrpc_server, $request_xml, '');
xmlrpc_server_destroy($xmlrpc_server);
?>


