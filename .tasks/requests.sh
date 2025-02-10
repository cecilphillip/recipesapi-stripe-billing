#!/bin/sh

# Text Output Formatting
YELLOW=$(tput setaf 3)
BLUE=$(tput setaf 12)
RED=$(tput setaf 1)
RESET=$(tput sgr0)

function authenticate() {
    echo "${YELLOW}Getting access token...${RESET}"
    response=$(curl -s -X POST ${API_HTTPS_URL}/connect/token -H "Content-Type: application/x-www-form-urlencoded" -d "grant_type=password&username=james@test.com&password=test")
    
    if [ $? -eq 0 ]; then
      access_token=$(echo $response | jq -r '.access_token')
      if [ "$access_token" != "null" ]; then
        #echo "${YELLOW}Access token retrieved:${RESET} $access_token"
        echo "${YELLOW}Access token retrieved${RESET}"
        export API_ACCESS_TOKEN=$access_token        
      else
        echo "Failed to retrieve access token"
      fi    
    else
      echo "Request failed"
    fi
}

function generate() {
    authenticate
    echo "${YELLOW}Making requests to ${API_HTTPS_URL}/api/recipes/random ...${RESET}"
    
    for i in $(seq 1 $ITERATIONS); do
      echo "${BLUE}Request ${i}...${RESET}"
      curl -X GET ${API_HTTPS_URL}/api/recipes/random -H "Accept: application/json" -H "Authorization: Bearer ${API_ACCESS_TOKEN}" -o /dev/null -s -S
     
      if [ $? -ne 0 ]; then
        echo "${RED}Request failed${RESET}"
      fi
    done
}

case $1 in
  authenticate)
    authenticate
    ;;
   generate)
      generate
      ;;
  *)
    echo "Nothing to see here"
    ;;
esac 