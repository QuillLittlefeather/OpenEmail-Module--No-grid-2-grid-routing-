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


