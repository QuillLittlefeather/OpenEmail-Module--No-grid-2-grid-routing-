CREATE TABLE IF NOT EXISTS `email` (
  `to` varchar(255) NOT NULL,
  `from` varchar(255) NOT NULL,
  `timestamp` int(10) NOT NULL,
  `region` varchar(255) NOT NULL,
  `objectname` varchar(255) NOT NULL,
  `objectlocation` varchar(255) NOT NULL,
  `subject` varchar(255) NOT NULL,
  `message` text NOT NULL
) ENGINE=MyISAM DEFAULT CHARSET=latin1;