#@echo off
#md tex
#rem bin\rsync.exe -rpztlv -L  rsync://contextgarden.net/minimals/current/misc/setuptex/ /cygdrive/c/Tools/MyContext/data2/tex/.

#md tex\texmf
#bin\rsync.exe -rpztlv -L --delete contextgarden.net::'minimals/current/base/tex/ minimals/current/base/metapost/ minimals/current/fonts/common/ minimals/current/fonts/other/ minimals/current/misc/web2c minimals/current/fonts/old/ minimals/current/fonts/new/' '/cygdrive/c/Tools/MyContext/data2/tex/texmf'
#bin\rsync.exe -rztlv -L --delete contextgarden.net::'minimals/current/bin/common/win64/ minimals/current/bin/context/win64/ minimals/current/bin/metapost/win64/ minimals/current/bin/luatex/win64/' '/cygdrive/c/Tools/MyContext/data2/tex/texmf-win64'

#md tex\texmf-context
#bin\rsync.exe -rztlv -L --delete  contextgarden.net::'minimals/current/context/beta/' '/cygdrive/c/Tools/MyContext/data2/tex/texmf-context'

#rem rsync.exe -n rsync://contextgarden.net/minimals/current/modules/
#md tex\texmf-modules
#bin\rsync.exe -rztlv -L --delete contextgarden.net::'minimals/current/modules/f-urwgaramond/ minimals/current/modules/f-urwgothic/ minimals/current/modules/t-account/ minimals/current/modules/t-algorithmic/ minimals/current/modules/t-animation/ minimals/current/modules/t-annotation/ minimals/current/modules/t-bnf/ minimals/current/modules/t-chromato/ minimals/current/modules/t-cmscbf/ minimals/current/modules/t-cmttbf/ minimals/current/modules/t-construction-plan/ minimals/current/modules/t-cyrillicnumbers/ minimals/current/modules/t-degrade/ minimals/current/modules/t-fancybreak/ minimals/current/modules/t-filter/ minimals/current/modules/t-fixme/ minimals/current/modules/t-french/ minimals/current/modules/t-fullpage/ minimals/current/modules/t-games/ minimals/current/modules/t-gantt/ minimals/current/modules/t-layout/ minimals/current/modules/t-letter/ minimals/current/modules/t-lettrine/ minimals/current/modules/t-lilypond/ minimals/current/modules/t-mathsets/ minimals/current/modules/t-rst/ minimals/current/modules/t-simpleslides/ minimals/current/modules/t-title/ minimals/current/modules/t-transliterator/ minimals/current/modules/t-typearea/ minimals/current/modules/t-typescripts/ minimals/current/modules/t-visualcounter/' '/cygdrive/c/Tools/MyContext/data2/tex/texmf-modules'

#rem copy andere rein



#rem update filename database for luatex
#rem C:\Tools\MyContext\data2\tex\texmf-win64\bin\mtxrun.exe --tree="c:\Tools\MyContext\data\tex"  --direct --resolve mktexlsr
#C:\Tools\MyContext\data2\tex\texmf-win64\bin\mtxrun.exe --tree="c:\Tools\MyContext\data2\tex" --generate
# nur en ist sinnvoll
#C:\Tools\MyContext\data2\tex\texmf-win64\bin\mtxrun.exe --tree="c:\Tools\MyContext\data2\tex"  --script context --autogenerate --make --engine=luatex cont-en cont-nl cont-cz cont-de cont-fa cont-it cont-ro cont-uk cont-pe
#C:\Tools\MyContext\data2\tex\texmf-win64\bin\mtxrun.exe --tree="c:\Tools\MyContext\data2\tex" --generate

#rem C:\Tools\MyContext\data2\tex\texmf-win64\bin\mtxrun.exe --autogenerate --script context --autopdf c:\temp\Angebot\A0003.tex
#rem  set PATH=C:\Tools\MyContext\data2\tex\texmf-win64\bin;%PATH%