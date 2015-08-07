# Configure Module


#Configure
Please use the format below for your lsl email settings

replace lsl.mygrid.com with lsl.yourdomainname.com

host_domain_header_from=lsl.mygrid.com
SMTP_internal_object_host=lsl.mygrid.com
SMTP_host_domain_header_from=lsl.mygrid.com

in Opensim.ini place the following configuration 
            [Email]
            EmailURL = http://mygrid.com/apps/email/xmlrpc.php
            host_domain_header_from=lsl.mygrid.com
            enabled=true
           ;enabled=true
            SMTP_internal_object_host=lsl.mygrid.com
            SMTP_host_domain_header_from=lsl.mygrid.com
           SMTP_SERVER_HOSTNAME=127.0.0.1
           SMTP_SERVER_PORT=25
           SMTP_SERVER_LOGIN=foo
           SMTP_SERVER_PASSWORD=bar
           
           
In OpenSim.ini under [startup] add 
emailmodule = OpenEmailModule
            
            
            
# How to use
Telling you how to script using llemail is beyond the scope of this readme. 
visit http://wiki.secondlife.com/wiki/LlEmail for scripting help.

To send email from one prim to another see example below.

The email address is object uuid the server script is in plus the grid email domain ex
to send email to aviworlds from say osgrid change @lsl.secondlife.com  to Primuuid@lsl.aviworlds.us



string version = "1"; //
string type = "lolcube";
default
{
    on_rez(integer start_param)
    {
        llEmail("5a634b27-f032-283f-2df2-55ead7724b23@lsl.secondlife.com",
            version,
            (string)llGetOwner() + "," + type);
    }
}


#The server:
default
{
    state_entry()
    {
        llSetTimerEvent(15.0);
    }
 
    timer()
    {
        llGetNextEmail("", "");
    }
 
    email( string time, string address, string version, string message, integer num_left )
    {    
        if ((integer)version < 2)
        {
            list info = llCSV2List( llDeleteSubString(message, 0, llSubStringIndex(message, "\n\n") + 1));
            llGiveInventory(llList2Key(info,0), llList2String(info,1));
        }
 
        if(num_left)
            llGetNextEmail("","");
    }
}
