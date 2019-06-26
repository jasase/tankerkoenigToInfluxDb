package main

import (
	"encoding/json"
	"fmt"
	"io/ioutil"
	"net/http"
)

type APIResult struct {
	ok      bool
	message string
	status  string
	data    string
	license string
}

func init() {
	fmt.Println("init Hello World")
}

func main() {
	fmt.Println("Hello World")

	response, err := http.Get("https://creativecommons.tankerkoenig.de/json/list.php?lat=0&lng=9.341125&rad=2&type=all&apikey=1cc54b68-f8d6-43ae-dda9-a6502617f8ec")

	if err != nil {
		fmt.Printf("Error %s\n", err)
	} else {

		if response.StatusCode != 200 {

			fmt.Println("Request not succesfull. Current status code ", response.StatusCode, " | ", response.Status)
			return
		}

		data, _ := ioutil.ReadAll(response.Body)

		var result APIResult
		errJSON := json.Unmarshal(data, &result)

		if errJSON != nil {
			fmt.Println(errJSON)
		}

		fmt.Println(string(data))

		if !result.ok {
			fmt.Println("Request not successful: " + result.message)
			return
		}

	}

	fmt.Println("Hello World")
}
