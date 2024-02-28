# WHAT IS THIS
a simple tool that:  
- initialises the needed stuff for silveraudioplayer's wave module  
- reads a file and outputs how many wave module wrappers can read said file's mimetype  
- initialises said wrappers and writes RAW PCM data from said wrappers with the file  

You can then import said file into a tool like Audacity, and i guess invert one track and downmix to see any differences. Different wrappers will read the file differently and will output in different formats, some pcm 16b, some pcm32b, some float32.