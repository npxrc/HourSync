# WORK IN PROGRESS
Still working on this file, it's not complete yet but it might be soon.. hopefully.

# How does HourSync work?


This file is dedicated to explaining how HourSync works and how it interacts with the eHours portal.

Each section corresponds to a .xaml/.xaml.cs file: e.x. the Login section corresponds to Login.xaml/Login.xaml.cs. Under each section will be a basic overview of what the file does and then a more detailed explanation will follow.

It's basically a documentation file.

Don't expect me to go over every single line of code. I don't feel like it and most of the code has comments and is easy to read. If it doesnt, then I've probably gone over it in this documentation

# Login

## Overview

Aside from submitting eHours, this might be the most complicated file. It's about 950 lines (idc if this number is exact).
Here's the basics of how logging in works:

1. Fake a Chrome browser (not necessarily needed but a precaution) by adding the Chrome user agent string
2. When the user clicks "Login", send the username and passsord to the student login page
3. Extract the cookies from the response and fetch the eHours page (not the useless home page)
4. Pass the list of eHours submissions and extracted cookies to Home

## In-Depth

This file does a TON more than just what the overview says. If the file was truly just what the overview said, Login would be like 300, _maybe_ 400 lines.

Login does the following:

-   Checks for updates
    -   Checks for internet
    -   Downloads updates if necessary and installs them
-   If its the first time opening the app, it will download a template of the settings from Firebase
-   Fetches previously saved credentials (if any) and tries to automatically log in (if setting is enabled)
    <br><br>

**Logging in**

1. Encodes user credentials into a Form
2. Makes a POST request to https://academyendorsement.olatheschools.com/loginuserstudent.php
3. Validates user credentials
4. If credentials are valid, makes a GET request to https://academyendorsement.olatheschools.com/Student/studentEHours.php using the session cookie to fetch eHour requests
5. Passes session cookie, credentials, and a few other things to Home

Lets look at the specifics now.

---

### POSTing

*Remember when I said this file was a work in progress... yeah*

A majority of this app was developed by me just looking at the DevTools network requests. Open inspect element and go to Network. It shows you every network request made after you open the Network tab.
