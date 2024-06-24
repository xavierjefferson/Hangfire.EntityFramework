﻿var term = new Terminal({ cols: 60, fontFamily: "Lucida Console, Courier New, Monospace" });
term.cols = 60;
//term.resize(60, 24);
term.open(document.getElementById("terminal"));
term.write("Please wait for log messages from our Hangfire events.  The first may take up to one minute...\r\n\r\n");
var p = window.setInterval(function() {
        term.write("Wait...\r\n");
    },
    5000);
this.connection = new signalR.HubConnectionBuilder()
    .withUrl("/chatHub")
    .configureLogging(2)
    .build();

this.connection
    .start()
    .then(() => {
        this.connection.invoke("GetRecentLog");
        this.connection.on("SendLogAsString",
            function(data) {
                if (p > 0) {
                    window.clearInterval(p);
                    p = 0;
                }
                term.writeln(data);
                console.log(data);
            });
        this.connection.on("SendLogAsObject",
            function(data) {
                if (p > 0) {
                    window.clearInterval(p);
                    p = 0;
                }
                term.write(`\x1B[31m${data.timestamp}`);
                term.write(" ");
                if (data.properties && data.properties.length) {
                    const a = JSON.parse(data.properties);
                    if (a.ThreadID) {
                        term.write(`\x1B[35m[${a.ThreadID}] `);
                    }
                    if (a.SourceContext) {
                        term.write(`\x1B[30m[${a.SourceContext}] `);
                    }
                }
                term.write(`\x1B[34m${data.level}`);
                term.write(" ");
                term.write(`\x1B[32m${data.message}`);
                term.write("\r\n");
                term.writeln("");
                //console.log(data);
            });
    })
    .catch(function(err) {
        console.error(err);
    });