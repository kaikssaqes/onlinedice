@echo off
echo ================================================
echo   Online Dice - deploy to GitHub Pages
echo ================================================
echo.

cd /d "C:\Users\kaikd\Downloads\onlinedice-deploy"

echo [1/3] Adding remote...
git remote remove origin 2>nul
git remote add origin https://github.com/kaikssaqes/onlinedice.git

echo [2/3] Pushing main branch...
git checkout main
git push -u origin main

echo [3/3] Pushing gh-pages branch...
git checkout gh-pages
git push -u origin gh-pages

echo.
echo ================================================
echo  DONE! Your live URL will be:
echo  https://kaikssaqes.github.io/onlinedice/
echo ================================================
echo.
echo  (It can take 1-2 minutes for GitHub Pages to build)
echo  Then go to your repo Settings - Pages and
echo  set Source = "Deploy from a branch" - "gh-pages" - "/root"
echo.
pause
