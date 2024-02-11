# SilverMagicBytes
A simple library to answer the age old question of which type is this file
or more specifically which type is this stream

It's independant from libmagic (for now) and does it's stuff in a semi efficient way.  
Open up MagicByteCombos (and maybe add some in a PR).

Q&A:  
Can the database of known file types and magic bytes be updated without the code being updated?  
No, not yet (well at least comfortably).  
Should I use this in my app?  
No, uh better alternatives exist.  
Why bother (when lib{Y} already exists}?  
It's purely c#(no native dependancies like libmagic), I know how it works, the sunk cost fallacy is real.  